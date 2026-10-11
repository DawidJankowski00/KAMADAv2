using Kamada.Shared.Matching.Drafts;

namespace Kamada.Shared.Matching.UnitTests;

public sealed class LlmOrderResponseTests
{
    [Fact]
    public void Parse_SchemaConformingAnswer_MapsEveryField()
    {
        var order = LlmOrderResponse.Parse("""
            {"client":{"name":"Jan","surname":"Kowalski","phone":"600100200","email":null},
             "address":{"street":"Grunwaldzka","buildingNumber":"15","apartmentNumber":"4","city":"Gdańsk","postalCode":null},
             "items":[{"product":"woda niegazowana 1,5 l","quantity":6},{"product":"chleb razowy","quantity":2}],
             "notes":"Proszę dzwonić domofonem"}
            """)!.ToDomain();

        order.ShouldBe(new ExtractedOrder(
            new ExtractedClient("Jan", "Kowalski", "600100200", null),
            new ExtractedAddress("Grunwaldzka", "15", "4", "Gdańsk", null),
            order.Lines,
            "Proszę dzwonić domofonem"));
        order.Lines.ShouldBe([new ExtractedLine("woda niegazowana 1,5 l", 6), new ExtractedLine("chleb razowy", 2)]);
    }

    [Fact]
    public void ToDomain_SloppyValues_AreCleaned()
    {
        var order = LlmOrderResponse.Parse("""
            {"client":{"name":"jan","surname":"kowalska-nowak","phone":"+48 600-100-200","email":" Jan.K @Example.com "},
             "address":{"street":" ","buildingNumber":"15 a","apartmentNumber":"null","city":"Gdańsk","postalCode":"80236"},
             "items":[{"product":"  ","quantity":3},{"product":"kawa","quantity":0}],
             "notes":"brak"}
            """)!.ToDomain();

        order.Client.ShouldBe(new ExtractedClient("Jan", "Kowalska-Nowak", "+48600100200", "jan.k@example.com"));
        order.Address.ShouldBe(new ExtractedAddress(null, "15A", null, "Gdańsk", "80-236"));
        order.Lines.ShouldBe([new ExtractedLine("kawa", 1)]);
        order.Notes.ShouldBeNull();
    }

    [Theory]
    [InlineData("Wojska Polskiego 70", null, null, "Wojska Polskiego", "70", null)]
    [InlineData("10 Lutego 20", null, null, "10 Lutego", "20", null)]
    [InlineData("Aleja 3 Maja 5a", "", null, "Aleja 3 Maja", "5A", null)]
    [InlineData("Ku Słońcu 50/59", null, null, "Ku Słońcu", "50", "59")]
    [InlineData("Grunwaldzka", "50/59", null, "Grunwaldzka", "50", "59")]
    [InlineData("Grunwaldzka", "50/59", "4", "Grunwaldzka", "50/59", "4")]
    [InlineData("Grunwaldzka 15", "15", "4", "Grunwaldzka 15", "15", "4")]
    [InlineData("10 Lutego", null, null, "10 Lutego", null, null)]
    [InlineData("Długa", "7", null, "Długa", "7", null)]
    public void ToDomain_NumbersInTheWrongAddressField_AreMovedWhereTheyBelong(
        string street, string? building, string? apartment, string expectedStreet, string? expectedBuilding, string? expectedApartment)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(new
        {
            address = new { street, buildingNumber = building, apartmentNumber = apartment, city = "Gdańsk", postalCode = (string?)null },
        });

        LlmOrderResponse.Parse(json)!.ToDomain().Address
            .ShouldBe(new ExtractedAddress(expectedStreet, expectedBuilding, expectedApartment, "Gdańsk", null));
    }

    [Theory]
    [InlineData("600 100", null)]
    [InlineData("nie podano", null)]
    public void ToDomain_TooShortPhone_IsDropped(string phone, string? expected) =>
        LlmOrderResponse.Parse($$"""{"client":{"phone":"{{phone}}"},"items":[]}""")!.ToDomain().Client.Phone.ShouldBe(expected);

    [Fact]
    public void ToDomain_InvalidEmail_IsDropped() =>
        LlmOrderResponse.Parse("""{"client":{"email":"jan kropka example"}}""")!.ToDomain().Client.Email.ShouldBeNull();

    [Fact]
    public void Parse_NumbersWhereStringsExpectedAndViceVersa_AreAccepted()
    {
        var order = LlmOrderResponse.Parse("""{"client":{"phone":600100200},"address":{"buildingNumber":15},"items":[{"product":"kawa","quantity":"2"}]}""")!.ToDomain();

        order.Client.Phone.ShouldBe("600100200");
        order.Address.BuildingNumber.ShouldBe("15");
        order.Lines.Single().Quantity.ShouldBe(2);
    }

    [Fact]
    public void Parse_AnswerInCodeFence_IsUnwrapped() =>
        LlmOrderResponse.Parse("```json\n{\"items\":[{\"product\":\"kawa\",\"quantity\":1}]}\n```").ShouldNotBeNull();

    [Fact]
    public void Parse_MissingSections_GiveAnEmptyExtraction() =>
        LlmOrderResponse.Parse("{}")!.ToDomain().IsEmpty.ShouldBeTrue();

    [Theory]
    [InlineData("")]
    [InlineData("Oto zamówienie: Jan Kowalski")]
    [InlineData("{\"items\": [")]
    [InlineData("[1, 2]")]
    public void Parse_NotJsonOfTheExpectedShape_ReturnsNull(string answer) =>
        LlmOrderResponse.Parse(answer).ShouldBeNull();
}
