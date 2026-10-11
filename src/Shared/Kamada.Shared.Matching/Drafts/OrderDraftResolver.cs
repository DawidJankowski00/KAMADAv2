using System.Text.RegularExpressions;
using Kamada.Shared.Contracts.Assistant;
using Kamada.Shared.Matching.Addresses;
using Kamada.Shared.Matching.Clients;
using Kamada.Shared.Matching.Products;
using Kamada.Shared.Matching.Text;

namespace Kamada.Shared.Matching.Drafts;

/// <summary>
/// Deterministic half of the assistant: matches what the model extracted against real clients, addresses and
/// products, and decides what will be selected and what will be created. Reads only – nothing is saved.
/// </summary>
public sealed partial class OrderDraftResolver(IOrderDraftSource source)
{
    public const int CandidateLimit = 3;

    /// <summary>A name match at least this similar (and the only one) selects the client.</summary>
    public const double StrongNameScore = 0.92;

    /// <summary>
    /// A product at least this similar selects itself, unless the runner-up is within <see cref="ProductScoreMargin"/>.
    /// A phrase fully covered by a longer catalogue name ("czajnik" → "Czajnik elektryczny 1,7 l") scores 0.85–0.9.
    /// </summary>
    public const double StrongProductScore = 0.85;

    public const double ProductScoreMargin = 0.05;

    /// <summary>Below this the spoken name clearly differs from the client found by phone/e-mail.</summary>
    public const double SamePersonNameScore = 0.75;

    /// <summary>The geocoder's street/city replaces the spoken one only when they are this similar.</summary>
    public const double SamePlaceNameScore = 0.85;

    public const int MaxQuantity = 10_000;
    public const string DefaultCountry = "Polska";

    public async Task<OrderDraftDto> ResolveAsync(ExtractedOrder order, string transcript, CancellationToken cancellationToken)
    {
        var warnings = new List<DraftWarningDto>();

        // Sequential on purpose: on the server the module contexts share one connection per request.
        var client = await ResolveClientAsync(order.Client, order.Address, warnings, cancellationToken);
        var address = await ResolveAddressAsync(order.Address, client, warnings, cancellationToken);
        var lines = await ResolveLinesAsync(order.Lines, warnings, cancellationToken);

        return new OrderDraftDto(client, address, lines, order.Notes, warnings, transcript);
    }

    /// <summary>"600100200" → "600 100 200"; other formats are kept as said.</summary>
    public static string? FormatPhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return null;
        }

        var digits = string.Concat(phone.Where(char.IsAsciiDigit));
        return digits.Length == 9 && !phone.TrimStart().StartsWith('+')
            ? $"{digits[..3]} {digits[3..6]} {digits[6..]}"
            : phone.Trim();
    }

    private async Task<DraftClientDto> ResolveClientAsync(
        ExtractedClient spoken,
        ExtractedAddress spokenAddress,
        List<DraftWarningDto> warnings,
        CancellationToken cancellationToken)
    {
        var mention = new ClientMention(spoken.Name, spoken.Surname, spoken.Phone, spoken.Email);
        if (mention.IsEmpty)
        {
            warnings.Add(new DraftWarningDto(DraftWarningCodes.ClientNotMentioned));
            return new DraftClientDto(DraftClientMode.New, null, [], await NewClientAsync(spoken, cancellationToken));
        }

        var found = await source.FindClientCandidatesAsync(mention, CandidateLimit, cancellationToken);
        var addressMention = ToMention(spokenAddress);
        var candidates = new List<ClientCandidateDto>(found.Count);
        foreach (var candidate in found)
        {
            var matchedAddress = addressMention.IsEmpty
                ? null
                : await source.FindShippingAddressAsync(candidate.Id, addressMention, cancellationToken);
            candidates.Add(new ClientCandidateDto(
                candidate.Id,
                $"{candidate.Name} {candidate.Surname} ({candidate.Email})",
                candidate.Score,
                candidate.Reason,
                matchedAddress));
        }

        // Phone number or e-mail identify a client with certainty.
        var certain = found.Where(c => c.Reason != ClientMatchReason.Name).ToList();
        if (certain.Count == 1)
        {
            WarnWhenNameDiffers(spoken, certain[0], warnings);
            return Existing(certain[0].Id, candidates);
        }

        if (certain.Count > 1)
        {
            warnings.Add(new DraftWarningDto(DraftWarningCodes.ClientAmbiguous));
            return new DraftClientDto(DraftClientMode.Ambiguous, null, candidates, await NewClientAsync(spoken, cancellationToken));
        }

        var strong = candidates.Where(c => c.Score >= StrongNameScore).ToList();
        if (strong.Count == 1)
        {
            return Existing(strong[0].Id, candidates);
        }

        if (strong.Count > 1)
        {
            // Namesakes: the spoken address may tell them apart.
            var atThatAddress = strong.Where(c => c.MatchedAddressId is not null).ToList();
            if (atThatAddress.Count == 1)
            {
                return Existing(atThatAddress[0].Id, candidates);
            }

            warnings.Add(new DraftWarningDto(DraftWarningCodes.ClientAmbiguous));
            return new DraftClientDto(DraftClientMode.Ambiguous, null, candidates, await NewClientAsync(spoken, cancellationToken));
        }

        // Only a weak name match ("Marta …"), but exactly one look-alike lives at the spoken address: that is the client.
        var onlyAtAddress = candidates.Where(c => c.MatchedAddressId is not null).ToList();
        if (onlyAtAddress.Count == 1)
        {
            return Existing(onlyAtAddress[0].Id, candidates);
        }

        // Nobody is similar enough: propose a new client, still listing look-alikes ("did you mean…?").
        return new DraftClientDto(DraftClientMode.New, null, candidates, await NewClientAsync(spoken, cancellationToken));
    }

    private async Task<DraftAddressDto> ResolveAddressAsync(
        ExtractedAddress spoken,
        DraftClientDto client,
        List<DraftWarningDto> warnings,
        CancellationToken cancellationToken)
    {
        if (spoken.IsEmpty)
        {
            if (client.Mode != DraftClientMode.Existing)
            {
                warnings.Add(new DraftWarningDto(DraftWarningCodes.AddressNotMentioned));
            }

            return new DraftAddressDto(DraftAddressMode.None, null, null);
        }

        var matchedId = client.Mode == DraftClientMode.Existing
            ? client.Candidates.Single(c => c.Id == client.SelectedId).MatchedAddressId
            : null;
        if (matchedId is not null)
        {
            return new DraftAddressDto(DraftAddressMode.Existing, matchedId, null);
        }

        return new DraftAddressDto(DraftAddressMode.New, null, await CompleteAddressAsync(spoken, warnings, cancellationToken));
    }

    /// <summary>
    /// Fills in what was not said (usually the postal code) and coordinates for a preview pin. A failed or doubtful
    /// lookup leaves the spoken values as they are – the admin completes them (geocoding never blocks the draft).
    /// </summary>
    private async Task<NewAddressDto> CompleteAddressAsync(
        ExtractedAddress spoken,
        List<DraftWarningDto> warnings,
        CancellationToken cancellationToken)
    {
        var address = new NewAddressDto(
            spoken.Street, spoken.BuildingNumber, spoken.ApartmentNumber, spoken.City, spoken.PostalCode, DefaultCountry, null, null);
        if (spoken.Street is null || (spoken.City is null && spoken.PostalCode is null))
        {
            return address;
        }

        var found = await source.GeocodeAsync(ToMention(spoken), DefaultCountry, cancellationToken);
        // Only a hit on the same building is trusted: a street-level match has a pin in the middle of the street and
        // possibly the postal code of another part of it.
        if (found is null
            || (found.Street is { } street && TextSimilarity.PhraseScore(spoken.Street, street) < SamePlaceNameScore)
            || (spoken.BuildingNumber is { } building && TextSimilarity.Compact(found.BuildingNumber) != TextSimilarity.Compact(building)))
        {
            warnings.Add(new DraftWarningDto(DraftWarningCodes.GeocodingFailed));
            return address;
        }

        return address with
        {
            Street = Prefer(spoken.Street, found.Street),
            City = spoken.City is null ? found.City : Prefer(spoken.City, found.City),
            PostalCode = spoken.PostalCode ?? (found.PostalCode is { } code && PostalCodePattern().IsMatch(code) ? code : null),
            Latitude = found.Latitude,
            Longitude = found.Longitude,
        };
    }

    private async Task<IReadOnlyList<DraftLineDto>> ResolveLinesAsync(
        IReadOnlyList<ExtractedLine> spoken,
        List<DraftWarningDto> warnings,
        CancellationToken cancellationToken)
    {
        if (spoken.Count == 0)
        {
            warnings.Add(new DraftWarningDto(DraftWarningCodes.NoProducts));
            return [];
        }

        var lines = new List<DraftLineDto>(spoken.Count);
        foreach (var line in spoken)
        {
            var quantity = Math.Clamp(line.Quantity, 1, MaxQuantity);
            var candidates = await source.FindProductCandidatesAsync(line.Product, CandidateLimit, cancellationToken);
            var selected = SelectProduct(candidates);

            if (candidates.Count == 0)
            {
                warnings.Add(new DraftWarningDto(DraftWarningCodes.ProductNotFound, line.Product));
            }
            else if (selected is null)
            {
                warnings.Add(new DraftWarningDto(DraftWarningCodes.ProductAmbiguous, line.Product));
            }
            else if (selected.StockQuantity < quantity)
            {
                warnings.Add(new DraftWarningDto(DraftWarningCodes.StockTooLow, selected.Name));
            }

            lines.Add(new DraftLineDto(
                line.Product,
                quantity,
                selected?.Id,
                [.. candidates.Select(c => new ProductCandidateDto(c.Id, c.Name, c.Sku, c.Price, c.StockQuantity, c.Score))]));
        }

        return lines;
    }

    private static ProductCandidate? SelectProduct(IReadOnlyList<ProductCandidate> candidates)
    {
        if (candidates.Count == 0 || candidates[0].Score < StrongProductScore)
        {
            return null;
        }

        return candidates.Count == 1 || candidates[1].Score <= candidates[0].Score - ProductScoreMargin ? candidates[0] : null;
    }

    private async Task<NewClientDto> NewClientAsync(ExtractedClient spoken, CancellationToken cancellationToken) => new(
        spoken.Name,
        spoken.Surname,
        FormatPhone(spoken.Phone),
        spoken.Email,
        await source.SuggestUsernameAsync(spoken.Name, spoken.Surname, cancellationToken));

    private static void WarnWhenNameDiffers(ExtractedClient spoken, ClientCandidate client, List<DraftWarningDto> warnings)
    {
        var spokenName = $"{spoken.Name} {spoken.Surname}".Trim();
        if (spokenName.Length > 0 && TextSimilarity.Similarity(spokenName, $"{client.Name} {client.Surname}") < SamePersonNameScore)
        {
            warnings.Add(new DraftWarningDto(DraftWarningCodes.ClientDetailsDiffer, spokenName));
        }
    }

    private static DraftClientDto Existing(Guid id, IReadOnlyList<ClientCandidateDto> candidates) =>
        new(DraftClientMode.Existing, id, candidates, null);

    private static string Prefer(string spoken, string? geocoded) =>
        geocoded is not null && TextSimilarity.PhraseScore(spoken, geocoded) >= SamePlaceNameScore ? geocoded : spoken;

    private static AddressMention ToMention(ExtractedAddress address) =>
        new(address.Street, address.BuildingNumber, address.ApartmentNumber, address.City, address.PostalCode);

    [GeneratedRegex("^[0-9]{2}-[0-9]{3}$")]
    private static partial Regex PostalCodePattern();
}
