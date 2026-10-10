namespace Kamada.Shared.Contracts.Assistant;

/// <param name="Enabled">Whether the admin panel shows the assistant at all.</param>
/// <param name="SpeechToText">Whether dictation is available (speech model loaded).</param>
/// <param name="Model">Language model used for extraction (e.g. <c>gemma3:4b</c>).</param>
public sealed record AssistantStatusDto(bool Enabled, bool SpeechToText, string? Model);

/// <param name="Language"><c>pl</c>, <c>en</c> or <c>auto</c> (default).</param>
public sealed record OrderDraftRequest(string Text, string? Language = null);

/// <summary>Result of <c>POST /api/assistant/transcriptions</c> (the recording itself is not stored).</summary>
/// <param name="Language">Recognized language, e.g. <c>pl</c>.</param>
/// <param name="DurationMs">Length of the recording.</param>
public sealed record TranscriptionDto(string Text, string Language, long DurationMs);

/// <summary>
/// Suggested order built from a dictated or typed note. Nothing has been saved: the admin reviews it and places the
/// order (creating the client/address first when <see cref="DraftClientMode.New"/> / <see cref="DraftAddressMode.New"/>).
/// </summary>
public sealed record OrderDraftDto(
    DraftClientDto Client,
    DraftAddressDto Address,
    IReadOnlyList<DraftLineDto> Lines,
    string? Notes,
    IReadOnlyList<DraftWarningDto> Warnings,
    string Transcript);

public enum DraftClientMode
{
    /// <summary>One existing client matched with confidence: <see cref="DraftClientDto.SelectedId"/>.</summary>
    Existing,

    /// <summary>Nobody matched: a new client will be created from <see cref="DraftClientDto.NewClient"/>.</summary>
    New,

    /// <summary>Several existing clients are similar: the admin picks one of the candidates or "new client".</summary>
    Ambiguous,
}

public enum DraftAddressMode
{
    /// <summary>An existing shipping address of the selected client: <see cref="DraftAddressDto.SelectedId"/>.</summary>
    Existing,

    /// <summary>A new shipping address will be added from <see cref="DraftAddressDto.NewAddress"/>.</summary>
    New,

    /// <summary>No address was mentioned: the admin chooses one of the client's addresses.</summary>
    None,
}

public enum ClientMatchReason
{
    Phone,
    Email,
    Name,
}

/// <param name="NewClient">Prefilled new client; present unless the mode is <see cref="DraftClientMode.Existing"/>.</param>
public sealed record DraftClientDto(
    DraftClientMode Mode,
    Guid? SelectedId,
    IReadOnlyList<ClientCandidateDto> Candidates,
    NewClientDto? NewClient);

/// <param name="Score">0 … 1; 1 = exact phone number or e-mail.</param>
/// <param name="MatchedAddressId">The candidate's shipping address equal to the mentioned one, if any.</param>
public sealed record ClientCandidateDto(
    Guid Id,
    string DisplayName,
    double Score,
    ClientMatchReason Reason,
    Guid? MatchedAddressId);

/// <summary>Values that were not said stay <c>null</c> – the admin fills them in (an e-mail is never invented).</summary>
public sealed record NewClientDto(string? Name, string? Surname, string? Phone, string? Email, string Username);

public sealed record DraftAddressDto(DraftAddressMode Mode, Guid? SelectedId, NewAddressDto? NewAddress);

/// <summary>Spoken address completed by the geocoder (postal code, coordinates for a preview pin) when possible.</summary>
public sealed record NewAddressDto(
    string? Street,
    string? BuildingNumber,
    string? ApartmentNumber,
    string? City,
    string? PostalCode,
    string Country,
    double? Latitude,
    double? Longitude);

/// <param name="SelectedProductId">Set when one product matched with confidence; otherwise the admin picks a candidate.</param>
public sealed record DraftLineDto(
    string SpokenText,
    int Quantity,
    Guid? SelectedProductId,
    IReadOnlyList<ProductCandidateDto> Candidates);

/// <param name="Score">0 … 1; 1 = exact name or SKU.</param>
public sealed record ProductCandidateDto(Guid Id, string Name, string Sku, decimal Price, int Stock, double Score);

/// <summary>Something the admin should check. <see cref="Code"/> is stable (see <see cref="DraftWarningCodes"/>).</summary>
/// <param name="Subject">What the warning is about, e.g. the spoken product phrase.</param>
public sealed record DraftWarningDto(string Code, string? Subject = null);

public static class DraftWarningCodes
{
    public const string ClientNotMentioned = "ClientNotMentioned";
    public const string ClientAmbiguous = "ClientAmbiguous";
    public const string ClientDetailsDiffer = "ClientDetailsDiffer";
    public const string AddressNotMentioned = "AddressNotMentioned";
    public const string GeocodingFailed = "GeocodingFailed";
    public const string ProductNotFound = "ProductNotFound";
    public const string ProductAmbiguous = "ProductAmbiguous";
    public const string StockTooLow = "StockTooLow";
    public const string NoProducts = "NoProducts";
}
