using Kamada.Modules.Assistant.Application.Drafts;
using Kamada.Modules.Assistant.Domain;
using Kamada.Modules.Catalog.Contracts;
using Kamada.Modules.Customers.Contracts;
using Kamada.Shared.Contracts.Assistant;
using Kamada.Shared.Infrastructure.Geocoding;
using Kamada.Shared.Kernel.Geography;
using CustomersMatchReason = Kamada.Modules.Customers.Contracts.ClientMatchReason;

namespace Kamada.Modules.Assistant.UnitTests;

public sealed class OrderDraftResolverTests
{
    private static readonly GeocodingResult Gdansk = new(
        new GeoPoint(54.38, 18.6), "Fake", null, new GeocodedAddress("Aleja Grunwaldzka", "15", "80-236", "Gdańsk"));

    private readonly FakeCustomerDirectory _customers = new();
    private readonly FakeCatalogLookup _catalog = new();
    private FakeGeocoder _geocoder = new(Gdansk);

    private static ExtractedClient Jan => new("Jan", "Kowalski", "600100200", null);

    private static ExtractedAddress Grunwaldzka => new("Grunwaldzka", "15", "4", "Gdańsk", null);

    [Fact]
    public async Task Resolve_PhoneMatch_SelectsClientAndTheirMatchingAddress()
    {
        var client = Candidate(1, CustomersMatchReason.Phone);
        var addressId = Guid.NewGuid();
        _customers.MatchingAddresses[client.Id] = addressId;

        var draft = await ResolveAsync(Order(Jan, Grunwaldzka));

        draft.Client.Mode.ShouldBe(DraftClientMode.Existing);
        draft.Client.SelectedId.ShouldBe(client.Id);
        draft.Client.NewClient.ShouldBeNull();
        draft.Address.ShouldBe(new DraftAddressDto(DraftAddressMode.Existing, addressId, null));
        _geocoder.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Resolve_PhoneMatchButDifferentName_WarnsTheAdmin()
    {
        Candidate(1, CustomersMatchReason.Phone, "Zofia", "Wiśniewska");

        var draft = await ResolveAsync(Order(Jan, ExtractedAddress.None));

        draft.Client.Mode.ShouldBe(DraftClientMode.Existing);
        draft.Warnings.ShouldContain(new DraftWarningDto(DraftWarningCodes.ClientDetailsDiffer, "Jan Kowalski"));
    }

    [Fact]
    public async Task Resolve_SingleStrongNameMatch_SelectsClient()
    {
        var client = Candidate(0.97, CustomersMatchReason.Name);
        Candidate(0.82, CustomersMatchReason.Name, "Jan", "Kowalczyk");

        var draft = await ResolveAsync(Order(Jan with { Phone = null }, ExtractedAddress.None));

        draft.Client.Mode.ShouldBe(DraftClientMode.Existing);
        draft.Client.SelectedId.ShouldBe(client.Id);
        draft.Client.Candidates.Count.ShouldBe(2);
        draft.Address.Mode.ShouldBe(DraftAddressMode.None);
        draft.Warnings.ShouldNotContain(w => w.Code == DraftWarningCodes.AddressNotMentioned);
    }

    [Fact]
    public async Task Resolve_TwoStrongNamesakes_IsAmbiguousWithNewClientOption()
    {
        Candidate(1, CustomersMatchReason.Name);
        Candidate(1, CustomersMatchReason.Name);

        var draft = await ResolveAsync(Order(Jan with { Phone = null }, ExtractedAddress.None));

        draft.Client.Mode.ShouldBe(DraftClientMode.Ambiguous);
        draft.Client.SelectedId.ShouldBeNull();
        draft.Client.Candidates.Count.ShouldBe(2);
        draft.Client.NewClient.ShouldNotBeNull();
        draft.Warnings.ShouldContain(w => w.Code == DraftWarningCodes.ClientAmbiguous);
    }

    [Fact]
    public async Task Resolve_NamesakesButOnlyOneLivesAtTheAddress_SelectsThatOne()
    {
        Candidate(1, CustomersMatchReason.Name);
        var atAddress = Candidate(1, CustomersMatchReason.Name);
        var addressId = Guid.NewGuid();
        _customers.MatchingAddresses[atAddress.Id] = addressId;

        var draft = await ResolveAsync(Order(Jan with { Phone = null }, Grunwaldzka));

        draft.Client.Mode.ShouldBe(DraftClientMode.Existing);
        draft.Client.SelectedId.ShouldBe(atAddress.Id);
        draft.Address.SelectedId.ShouldBe(addressId);
        draft.Client.Candidates.Single(c => c.Id == atAddress.Id).MatchedAddressId.ShouldBe(addressId);
    }

    [Fact]
    public async Task Resolve_FirstNameOnlyButOneLookAlikeLivesAtTheAddress_SelectsThatClient()
    {
        Candidate(0.8, CustomersMatchReason.Name, "Marta", "Kamińska");
        var atAddress = Candidate(0.8, CustomersMatchReason.Name, "Marta", "Jaworska");
        var addressId = Guid.NewGuid();
        _customers.MatchingAddresses[atAddress.Id] = addressId;

        var draft = await ResolveAsync(Order(new ExtractedClient("Marta", null, null, null), Grunwaldzka));

        draft.Client.Mode.ShouldBe(DraftClientMode.Existing);
        draft.Client.SelectedId.ShouldBe(atAddress.Id);
        draft.Address.SelectedId.ShouldBe(addressId);
    }

    [Fact]
    public async Task Resolve_OnlyWeakNameMatches_ProposesNewClientButListsLookAlikes()
    {
        var lookAlike = Candidate(0.85, CustomersMatchReason.Name, "Jan", "Kowalczyk");
        _customers.TakenUsernames.Add("jan.kowalski");

        var draft = await ResolveAsync(Order(Jan, Grunwaldzka));

        draft.Client.Mode.ShouldBe(DraftClientMode.New);
        draft.Client.Candidates.Single().Id.ShouldBe(lookAlike.Id);
        draft.Client.NewClient.ShouldBe(new NewClientDto("Jan", "Kowalski", "600 100 200", null, "jan.kowalski2"));
    }

    [Fact]
    public async Task Resolve_NewAddress_IsCompletedByTheGeocoder()
    {
        var draft = await ResolveAsync(Order(Jan, Grunwaldzka));

        draft.Address.Mode.ShouldBe(DraftAddressMode.New);
        draft.Address.NewAddress.ShouldBe(new NewAddressDto("Aleja Grunwaldzka", "15", "4", "Gdańsk", "80-236", "Polska", 54.38, 18.6));
        _geocoder.Requests.Single().PostalCode.ShouldBeEmpty();
        draft.Warnings.ShouldNotContain(w => w.Code == DraftWarningCodes.GeocodingFailed);
    }

    [Fact]
    public async Task Resolve_SpokenPostalCode_IsKept()
    {
        var draft = await ResolveAsync(Order(Jan, Grunwaldzka with { PostalCode = "80-001" }));

        draft.Address.NewAddress!.PostalCode.ShouldBe("80-001");
    }

    [Fact]
    public async Task Resolve_GeocoderFindsNothing_KeepsSpokenAddressAndWarns()
    {
        _geocoder = new FakeGeocoder(null);

        var draft = await ResolveAsync(Order(Jan, Grunwaldzka));

        draft.Address.NewAddress.ShouldBe(new NewAddressDto("Grunwaldzka", "15", "4", "Gdańsk", null, "Polska", null, null));
        draft.Warnings.ShouldContain(new DraftWarningDto(DraftWarningCodes.GeocodingFailed));
    }

    [Fact]
    public async Task Resolve_GeocoderFindsADifferentStreet_IgnoresItAndWarns()
    {
        _geocoder = new FakeGeocoder(Gdansk with { Address = new GeocodedAddress("Kartuska", "15", "80-100", "Gdańsk") });

        var draft = await ResolveAsync(Order(Jan, Grunwaldzka));

        draft.Address.NewAddress!.PostalCode.ShouldBeNull();
        draft.Address.NewAddress.Latitude.ShouldBeNull();
        draft.Warnings.ShouldContain(new DraftWarningDto(DraftWarningCodes.GeocodingFailed));
    }

    [Fact]
    public async Task Resolve_GeocoderFindsOnlyTheStreet_DoesNotTrustPostalCodeOrPin()
    {
        _geocoder = new FakeGeocoder(Gdansk with { Address = new GeocodedAddress("Aleja Grunwaldzka", null, "80-241", "Gdańsk") });

        var draft = await ResolveAsync(Order(Jan, Grunwaldzka));

        draft.Address.NewAddress!.PostalCode.ShouldBeNull();
        draft.Address.NewAddress.Latitude.ShouldBeNull();
        draft.Warnings.ShouldContain(new DraftWarningDto(DraftWarningCodes.GeocodingFailed));
    }

    [Fact]
    public async Task Resolve_AddressWithoutCity_IsNotGeocoded()
    {
        await ResolveAsync(Order(Jan, Grunwaldzka with { City = null }));

        _geocoder.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Resolve_NothingAboutTheClient_ProposesEmptyNewClientAndWarns()
    {
        var draft = await ResolveAsync(Order(ExtractedClient.None, ExtractedAddress.None));

        draft.Client.Mode.ShouldBe(DraftClientMode.New);
        draft.Warnings.Select(w => w.Code).ShouldContain(DraftWarningCodes.ClientNotMentioned);
        draft.Warnings.Select(w => w.Code).ShouldContain(DraftWarningCodes.AddressNotMentioned);
        _customers.LastMention.ShouldBeNull();
    }

    [Fact]
    public async Task Resolve_ClearProductMatch_SelectsItAndWarnsAboutLowStock()
    {
        var bread = Product("chleb razowy", "Chleb razowy 500 g", 1, stock: 3);

        var draft = await ResolveAsync(Order(Jan, ExtractedAddress.None, new ExtractedLine("chleb razowy", 5)));

        draft.Lines.Single().SelectedProductId.ShouldBe(bread.Id);
        draft.Lines.Single().Quantity.ShouldBe(5);
        draft.Warnings.ShouldContain(new DraftWarningDto(DraftWarningCodes.StockTooLow, "Chleb razowy 500 g"));
    }

    [Fact]
    public async Task Resolve_RunnerUpWithinMargin_LeavesProductUnselected()
    {
        Product("woda", "Woda gazowana", 0.95);
        Product("woda", "Woda niegazowana", 0.93);

        var draft = await ResolveAsync(Order(Jan, ExtractedAddress.None, new ExtractedLine("woda", 6)));

        draft.Lines.Single().SelectedProductId.ShouldBeNull();
        draft.Lines.Single().Candidates.Count.ShouldBe(2);
        draft.Warnings.ShouldContain(new DraftWarningDto(DraftWarningCodes.ProductAmbiguous, "woda"));
    }

    [Fact]
    public async Task Resolve_RunnerUpFarBehind_SelectsTheBestProduct()
    {
        var still = Product("woda niegazowana", "Woda niegazowana", 0.98);
        Product("woda niegazowana", "Woda gazowana", 0.85);

        var draft = await ResolveAsync(Order(Jan, ExtractedAddress.None, new ExtractedLine("woda niegazowana", 6)));

        draft.Lines.Single().SelectedProductId.ShouldBe(still.Id);
    }

    [Fact]
    public async Task Resolve_UnknownProduct_KeepsTheLineAndWarns()
    {
        var draft = await ResolveAsync(Order(Jan, ExtractedAddress.None, new ExtractedLine("pralka", 1)));

        var line = draft.Lines.Single();
        (line.SpokenText, line.Quantity, line.SelectedProductId).ShouldBe(("pralka", 1, (Guid?)null));
        line.Candidates.ShouldBeEmpty();
        draft.Warnings.ShouldContain(new DraftWarningDto(DraftWarningCodes.ProductNotFound, "pralka"));
    }

    [Fact]
    public async Task Resolve_HugeQuantity_IsCapped()
    {
        Product("mata", "Mata", 1, stock: 100_000);

        var draft = await ResolveAsync(Order(Jan, ExtractedAddress.None, new ExtractedLine("mata", 1_000_000)));

        draft.Lines.Single().Quantity.ShouldBe(OrderDraftResolver.MaxQuantity);
    }

    [Theory]
    [InlineData("600100200", "600 100 200")]
    [InlineData("+48600100200", "+48600100200")]
    [InlineData(null, null)]
    public void FormatPhone_GroupsNationalNumbers(string? phone, string? expected) =>
        OrderDraftResolver.FormatPhone(phone).ShouldBe(expected);

    private Task<OrderDraftDto> ResolveAsync(ExtractedOrder order) =>
        new OrderDraftResolver(_customers, _catalog, _geocoder).ResolveAsync(order, "notatka", TestContext.Current.CancellationToken);

    private static ExtractedOrder Order(ExtractedClient client, ExtractedAddress address, params ExtractedLine[] lines) =>
        new(client, address, lines, null);

    private ClientCandidate Candidate(double score, CustomersMatchReason reason, string name = "Jan", string surname = "Kowalski")
    {
        var candidate = new ClientCandidate(Guid.NewGuid(), name, surname, $"{Guid.NewGuid():N}@example.com", "600 100 200", score, reason);
        _customers.Candidates.Add(candidate);
        return candidate;
    }

    private ProductCandidate Product(string phrase, string name, double score, int stock = 100)
    {
        var product = new ProductCandidate(Guid.NewGuid(), name, name.ToUpperInvariant(), 9.99m, stock, score);
        if (!_catalog.Candidates.TryGetValue(phrase, out var list))
        {
            _catalog.Candidates[phrase] = list = [];
        }

        list.Add(product);
        return product;
    }
}
