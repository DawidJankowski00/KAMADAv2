using Kamada.Shared.Contracts.Assistant;
using Kamada.Shared.Contracts.Catalog;
using Kamada.Shared.Contracts.Customers;
using Kamada.Shared.Matching.Drafts;
using Kamada.Web.Shared.Assistant;
using Kamada.Web.Shared.Assistant.Evaluation;

namespace Kamada.Web.Shared.UnitTests.Assistant;

/// <summary>The phone's draft: the model's answer (as JSON) matched against the local copy by the shared resolver.</summary>
public sealed class LocalDraftTests
{
    private static readonly AddressDto Grunwaldzka = new(
        Guid.NewGuid(), AddressType.Shipping, "Aleja Grunwaldzka", "15", "4", "Gdańsk", "80-236", "Polska", 54.38, 18.6, null, null);

    private static readonly ClientLookupDto Jan = new(Guid.NewGuid(), "jan.kowalski", "jan@example.com", "Jan", "Kowalski", "600 100 200", [Grunwaldzka]);

    private static readonly ProductLookupDto Water = new(Guid.NewGuid(), "WODA-NG", "Woda niegazowana 1,5 l", 2.49m, 120, []);
    private static readonly ProductLookupDto Bread = new(Guid.NewGuid(), "CHLEB-RAZ", "Chleb razowy", 6.99m, 15, []);

    private const string Answer = """
        {"client":{"name":"Jan","surname":"Kowalski","phone":null,"email":null},
         "address":{"street":"Grunwaldzka","buildingNumber":"15","apartmentNumber":"4","city":"Gdańsk","postalCode":null},
         "items":[{"product":"woda niegazowana 1,5 l","quantity":6},{"product":"chleb razowy","quantity":2}],"notes":null}
        """;

    [Fact]
    public async Task Resolve_AnswerAboutKnownClient_SelectsClientAddressAndProductsFromTheLocalCopy()
    {
        var draft = await DraftAsync(Answer);

        draft.Client.Mode.ShouldBe(DraftClientMode.Existing);
        draft.Client.SelectedId.ShouldBe(Jan.Id);
        draft.Address.ShouldBe(new DraftAddressDto(DraftAddressMode.Existing, Grunwaldzka.Id, null));
        draft.Lines.Select(l => l.SelectedProductId).ShouldBe([Water.Id, Bread.Id]);
        draft.Lines.Select(l => l.Quantity).ShouldBe([6, 2]);
    }

    [Fact]
    public async Task Resolve_UnknownClientWithNewAddress_ProposesNewOnesWithoutGeocoding()
    {
        var draft = await DraftAsync("""
            {"client":{"name":"Anna","surname":"Nowak","phone":"512345678","email":null},
             "address":{"street":"Długa","buildingNumber":"7","apartmentNumber":null,"city":"Gdańsk","postalCode":null},
             "items":[],"notes":null}
            """);

        draft.Client.Mode.ShouldBe(DraftClientMode.New);
        draft.Client.NewClient.ShouldBe(new NewClientDto("Anna", "Nowak", "512 345 678", null, "anna.nowak"));
        draft.Address.NewAddress.ShouldNotBeNull().PostalCode.ShouldBeNull();
        draft.Warnings.ShouldContain(w => w.Code == DraftWarningCodes.GeocodingFailed);
        draft.Warnings.ShouldContain(w => w.Code == DraftWarningCodes.NoProducts);
    }

    [Fact]
    public async Task Resolve_ProductMissingFromTheLocalCopy_IsLeftForTheWorkerToChoose()
    {
        var draft = await DraftAsync("""{"client":{"name":"Jan","surname":"Kowalski"},"address":null,"items":[{"product":"pralka","quantity":1}]}""");

        draft.Lines.Single().SelectedProductId.ShouldBeNull();
        draft.Warnings.ShouldContain(new DraftWarningDto(DraftWarningCodes.ProductNotFound, "pralka"));
    }

    [Fact]
    public async Task Score_CorrectDraft_IsAllCorrect()
    {
        var draft = await DraftAsync(Answer);
        var evalCase = new AssistantEvalCase(
            "x", "pl", "text", "Existing:Jan Kowalski", "Existing:Aleja Grunwaldzka 15",
            [new AssistantEvalLine("WODA-NG", null, false, 6), new AssistantEvalLine("CHLEB-RAZ", null, false, 2)]);

        var result = AssistantEvalScorer.Evaluate(evalCase, draft, Jan, 1200);

        result.AllCorrect.ShouldBeTrue(string.Join("; ", result.Problems));
    }

    [Fact]
    public async Task Score_WrongProductAndQuantity_ReportsBoth()
    {
        var draft = await DraftAsync(Answer);
        var evalCase = new AssistantEvalCase(
            "x", "pl", "text", "Existing:Jan Kowalski", "None",
            [new AssistantEvalLine("CHLEB-RAZ", null, false, 6), new AssistantEvalLine("CHLEB-RAZ", null, false, 2)]);

        var result = AssistantEvalScorer.Evaluate(evalCase, draft, Jan, 1200);

        result.AllCorrect.ShouldBeFalse();
        (result.ClientCorrect, result.AddressCorrect, result.ProductsCorrect, result.QuantitiesCorrect).ShouldBe((true, false, 1, 2));
        result.Problems.ShouldContain(p => p.StartsWith("address: expected None", StringComparison.Ordinal));
    }

    private static async Task<OrderDraftDto> DraftAsync(string modelAnswer)
    {
        var extracted = ExtractionAnswer.Parse(modelAnswer).ShouldNotBeNull();
        var source = new LocalDraftSource([Jan], [Water, Bread]);
        return await new OrderDraftResolver(source).ResolveAsync(extracted, "notatka", TestContext.Current.CancellationToken);
    }
}
