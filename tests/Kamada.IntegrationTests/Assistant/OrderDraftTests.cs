using Kamada.Shared.Matching.Drafts;
using System.Net;
using Kamada.IntegrationTests.Infrastructure;
using Kamada.Modules.Assistant.Domain;
using Kamada.Modules.Workforce.Domain.Workers;
using Kamada.Shared.Contracts.Assistant;
using Kamada.Shared.Contracts.Catalog;
using Kamada.Shared.Contracts.Customers;
using Kamada.Shared.Contracts.Ordering;
using Microsoft.AspNetCore.Hosting;

namespace Kamada.IntegrationTests.Assistant;

/// <summary>
/// The language model is replaced with <see cref="FakeOrderExtractor"/>; everything after extraction (matching against
/// real clients, addresses and products, geocoding completion, the admin's accept flow) runs for real.
/// Names are random words, so other tests' data in the shared database never matches.
/// </summary>
public sealed class OrderDraftTests(KamadaApiFactory factory)
{
    private const string DraftsUrl = "/api/assistant/order-drafts";

    [Fact]
    public async Task Status_AsAdmin_ReportsEnabledAssistantAndModel()
    {
        var (admin, _) = await factory.CreateSignedInClientAsync(WorkerRole.Admin);

        var status = await (await admin.GetAsync("/api/assistant/status", TestContext.Current.CancellationToken)).ReadAsAsync<AssistantStatusDto>();

        status.Enabled.ShouldBeTrue();
        status.Model.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task CreateDraft_KnownPhoneAndAddress_SelectsExistingClientAddressAndProduct()
    {
        var (admin, _) = await factory.CreateSignedInClientAsync(WorkerRole.Admin);
        var product = await CreateNamedProductAsync(admin, $"Sok {Word()} jabłkowy");
        var client = await CreateClientAsync(admin, "Jan", Word(), new AddressRequest(AddressType.Shipping, "Grunwaldzka", "15", "4", "Gdańsk", "80-236"));
        var text = Note();
        factory.OrderExtractor.Register(text, new ExtractedOrder(
            new ExtractedClient("Jan", client.Surname, Digits(client.PhoneNumber), null),
            new ExtractedAddress("Grunwaldzka", "15", "4", "Gdańsk", null),
            [new ExtractedLine(product.Name.ToLowerInvariant(), 2)],
            "Proszę dzwonić domofonem"));

        var draft = await CreateDraftAsync(admin, text);

        draft.Client.Mode.ShouldBe(DraftClientMode.Existing);
        draft.Client.SelectedId.ShouldBe(client.Id);
        draft.Client.Candidates[0].Reason.ShouldBe(ClientMatchReason.Phone);
        draft.Address.Mode.ShouldBe(DraftAddressMode.Existing);
        draft.Address.SelectedId.ShouldBe(client.Addresses.Single().Id);
        draft.Lines.Single().SelectedProductId.ShouldBe(product.Id);
        draft.Lines.Single().Quantity.ShouldBe(2);
        draft.Notes.ShouldBe("Proszę dzwonić domofonem");
        draft.Transcript.ShouldBe(text);
        draft.Warnings.ShouldBeEmpty();
    }

    [Fact]
    public async Task CreateDraft_UnknownClient_ProposesNewClientAndGeocodedAddress()
    {
        var (admin, _) = await factory.CreateSignedInClientAsync(WorkerRole.Admin);
        var surname = Capitalized(Word());
        var text = Note();
        factory.OrderExtractor.Register(text, new ExtractedOrder(
            new ExtractedClient("Anna", surname, "600100200", null),
            new ExtractedAddress("Wały Chrobrego", "12", null, "Szczecin", null),
            [],
            null));

        var draft = await CreateDraftAsync(admin, text);

        draft.Client.Mode.ShouldBe(DraftClientMode.New);
        draft.Client.NewClient.ShouldNotBeNull();
        draft.Client.NewClient.Name.ShouldBe("Anna");
        draft.Client.NewClient.Phone.ShouldBe("600 100 200");
        draft.Client.NewClient.Email.ShouldBeNull();
        draft.Client.NewClient.Username.ShouldBe($"anna.{surname.ToLowerInvariant()}");
        draft.Address.Mode.ShouldBe(DraftAddressMode.New);
        draft.Address.NewAddress.ShouldNotBeNull();
        draft.Address.NewAddress.PostalCode.ShouldBe(FakeGeocodingService.PostalCode);
        draft.Address.NewAddress.Latitude.ShouldNotBeNull();
        draft.Warnings.Select(w => w.Code).ShouldBe([DraftWarningCodes.NoProducts]);
    }

    [Fact]
    public async Task CreateDraft_TwoEquallySimilarProducts_LeavesTheChoiceToTheAdmin()
    {
        var (admin, _) = await factory.CreateSignedInClientAsync(WorkerRole.Admin);
        var word = Word();
        var sparkling = await CreateNamedProductAsync(admin, $"Woda {word} gazowana");
        var still = await CreateNamedProductAsync(admin, $"Woda {word} niegazowana");
        var text = Note();
        factory.OrderExtractor.Register(text, new ExtractedOrder(
            ExtractedClient.None, ExtractedAddress.None, [new ExtractedLine($"woda {word}", 6)], null));

        var draft = await CreateDraftAsync(admin, text);

        var line = draft.Lines.Single();
        line.SelectedProductId.ShouldBeNull();
        // Products of other tests share the database and a random name may pass the candidate threshold too.
        line.Candidates.Take(2).Select(c => c.Id).ShouldBe([sparkling.Id, still.Id], ignoreOrder: true);
        draft.Warnings.ShouldContain(new DraftWarningDto(DraftWarningCodes.ProductAmbiguous, $"woda {word}"));
        draft.Warnings.ShouldContain(new DraftWarningDto(DraftWarningCodes.ClientNotMentioned));
    }

    [Fact]
    public async Task AcceptDraft_NewClient_CreatesClientWithAddressAndPlacesTheOrder()
    {
        var (admin, _) = await factory.CreateSignedInClientAsync(WorkerRole.Admin);
        var product = await CreateNamedProductAsync(admin, $"Chleb {Word()} razowy", stock: 5);
        var surname = Capitalized(Word());
        var text = Note();
        factory.OrderExtractor.Register(text, new ExtractedOrder(
            new ExtractedClient("Ewa", surname, "511222333", null),
            new ExtractedAddress("Długa", "7", "2", "Gdańsk", null),
            [new ExtractedLine(product.Name, 2)],
            null));
        var draft = await CreateDraftAsync(admin, text);

        // What the admin panel does on "Utwórz klienta i złóż zamówienie" (the admin typed the missing e-mail).
        var newClient = draft.Client.NewClient!;
        var newAddress = draft.Address.NewAddress!;
        var created = await admin.PostJsonAsync("/api/clients", new CreateClientRequest(
            newClient.Username,
            $"{newClient.Username}@example.com",
            newClient.Name!,
            newClient.Surname!,
            newClient.Phone!,
            [new AddressRequest(AddressType.Shipping, newAddress.Street!, newAddress.BuildingNumber!, newAddress.ApartmentNumber, newAddress.City!, newAddress.PostalCode!, newAddress.Country)]));
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var client = await created.ReadAsAsync<ClientDto>();
        var placed = await admin.PlaceOrderRawAsync(client.Id, client.Addresses.Single().Id, (draft.Lines.Single().SelectedProductId!.Value, draft.Lines.Single().Quantity));

        placed.StatusCode.ShouldBe(HttpStatusCode.Created);
        var order = await placed.ReadAsAsync<OrderDto>();
        order.ShippingAddress.RecipientName.ShouldBe($"Ewa {surname}");
        order.ShippingAddress.PostalCode.ShouldBe(FakeGeocodingService.PostalCode);
        order.ShippingAddress.Latitude.ShouldNotBeNull();
        order.Items.Single().Quantity.ShouldBe(2);
    }

    [Fact]
    public async Task CreateDraft_UnusableModelAnswer_Returns400ExtractionFailed()
    {
        var (admin, _) = await factory.CreateSignedInClientAsync(WorkerRole.Admin);

        var response = await admin.PostJsonAsync(DraftsUrl, new OrderDraftRequest(Note()));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.ReadProblemCodeAsync()).ShouldBe("Assistant.ExtractionFailed");
    }

    [Fact]
    public async Task CreateDraft_NothingRecognized_Returns400()
    {
        var (admin, _) = await factory.CreateSignedInClientAsync(WorkerRole.Admin);
        var text = Note();
        factory.OrderExtractor.Register(text, new ExtractedOrder(ExtractedClient.None, ExtractedAddress.None, [], "tylko uwaga"));

        var response = await admin.PostJsonAsync(DraftsUrl, new OrderDraftRequest(text));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.ReadProblemCodeAsync()).ShouldBe("Assistant.NothingRecognized");
    }

    [Theory]
    [InlineData("", null)]
    [InlineData("ok", "de")]
    public async Task CreateDraft_InvalidRequest_Returns400(string text, string? language)
    {
        var (admin, _) = await factory.CreateSignedInClientAsync(WorkerRole.Admin);

        var response = await admin.PostJsonAsync(DraftsUrl, new OrderDraftRequest(text, language));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateDraft_TextOverLimit_Returns400()
    {
        var (admin, _) = await factory.CreateSignedInClientAsync(WorkerRole.Admin);

        var response = await admin.PostJsonAsync(DraftsUrl, new OrderDraftRequest(new string('a', 2001)));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateDraft_AsCourier_Returns403()
    {
        var (courier, _) = await factory.CreateSignedInClientAsync(WorkerRole.Courier);

        var response = await courier.PostJsonAsync(DraftsUrl, new OrderDraftRequest(Note()));

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateDraft_WhenAssistantIsDisabled_Returns503AndStatusSaysSo()
    {
        await using var disabled = factory.WithWebHostBuilder(builder => builder.UseSetting("Assistant:Enabled", "false"));
        var admin = await SignInAsync(disabled);

        var response = await admin.PostJsonAsync(DraftsUrl, new OrderDraftRequest(Note()));
        var status = await (await admin.GetAsync("/api/assistant/status", TestContext.Current.CancellationToken)).ReadAsAsync<AssistantStatusDto>();

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        (await response.ReadProblemCodeAsync()).ShouldBe("Assistant.Disabled");
        status.Enabled.ShouldBeFalse();
    }

    [Fact]
    public async Task CreateDraft_AfterPermitLimitExceeded_Returns429()
    {
        // Separate host with its own limiter state, so other tests are not throttled.
        await using var limited = factory.WithWebHostBuilder(builder => builder.UseSetting("RateLimiting:Assistant:PermitLimit", "2"));
        var admin = await SignInAsync(limited);

        for (var attempt = 0; attempt < 2; attempt++)
        {
            (await admin.PostJsonAsync(DraftsUrl, new OrderDraftRequest(Note()))).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }

        var blocked = await admin.PostJsonAsync(DraftsUrl, new OrderDraftRequest(Note()));

        blocked.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }

    private async Task<HttpClient> SignInAsync(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> host)
    {
        var worker = await factory.CreateWorkerAsync(WorkerRole.Admin);
        var client = host.CreateClient();
        var tokens = await client.LoginAsync(worker.Username, worker.Password);
        return client.WithBearer(tokens.AccessToken);
    }

    private static async Task<OrderDraftDto> CreateDraftAsync(HttpClient admin, string text)
    {
        var response = await admin.PostJsonAsync(DraftsUrl, new OrderDraftRequest(text, "pl"));
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await response.ReadAsAsync<OrderDraftDto>();
    }

    private static async Task<ProductDto> CreateNamedProductAsync(HttpClient admin, string name, int stock = 50)
    {
        var sku = $"AS-{Guid.NewGuid():N}"[..15].ToUpperInvariant();
        var response = await admin.PostJsonAsync("/api/products", new CreateProductRequest(name, null, sku, 4.99m, stock, []));
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return await response.ReadAsAsync<ProductDto>();
    }

    private static async Task<ClientDto> CreateClientAsync(HttpClient admin, string name, string surname, AddressRequest address)
    {
        var username = $"{name}.{surname}".ToLowerInvariant();
        var phone = "5" + string.Concat(Guid.NewGuid().ToByteArray().Take(8).Select(b => (char)('0' + (b % 10))));
        var response = await admin.PostJsonAsync("/api/clients", new CreateClientRequest(
            username, $"{username}@example.com", name, Capitalized(surname), phone, [address]));
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return await response.ReadAsAsync<ClientDto>();
    }

    /// <summary>A random lower-case word (no digits), unlike any name another test creates.</summary>
    private static string Word() => string.Concat(Guid.NewGuid().ToByteArray().Take(9).Select(b => (char)('a' + (b % 26))));

    private static string Capitalized(string word) => char.ToUpperInvariant(word[0]) + word[1..];

    private static string Note() => $"notatka {Guid.NewGuid():N}";

    private static string Digits(string phone) => string.Concat(phone.Where(char.IsAsciiDigit));
}
