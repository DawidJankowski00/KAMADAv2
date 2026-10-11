using Kamada.Shared.Contracts.Catalog;
using Kamada.Shared.Contracts.Customers;
using Kamada.Web.Shared.Offline;

namespace Kamada.Web.Shared.UnitTests.Offline;

public sealed class LocalSearchTests
{
    private static readonly ClientLookupDto Łukasz = Client("Łukasz", "Wójcik", "lukasz.w@example.com", "+48 601 234 567");
    private static readonly ClientLookupDto Anna = Client("Anna", "Nowak", "anna@example.com", "+48 500 100 200");

    [Theory]
    [InlineData("wojcik")]
    [InlineData("Łukasz Wójcik")]
    [InlineData("lukasz woj")]
    [InlineData("601234")]
    [InlineData("601 234")]
    [InlineData("LUKASZ.W@")]
    public void Clients_MatchesIgnoringCaseAndDiacritics(string text) =>
        LocalSearch.Clients([Anna, Łukasz], text).ShouldBe([Łukasz]);

    [Fact]
    public void Clients_EveryWordMustMatch() =>
        LocalSearch.Clients([Anna, Łukasz], "anna wójcik").ShouldBeEmpty();

    [Fact]
    public void Clients_EmptyText_ReturnsEveryoneSortedBySurname() =>
        LocalSearch.Clients([Łukasz, Anna], " ").ShouldBe([Anna, Łukasz]);

    [Fact]
    public void Products_MatchesNameSkuOrCategory()
    {
        var water = new ProductLookupDto(Guid.NewGuid(), "NAP-WODA-15", "Woda niegazowana 1,5 l", 2.50m, 40, [new CategoryRefDto(Guid.NewGuid(), "Napoje")]);
        var bread = new ProductLookupDto(Guid.NewGuid(), "PIE-CHLEB", "Chleb razowy", 5.90m, 10, [new CategoryRefDto(Guid.NewGuid(), "Pieczywo")]);

        LocalSearch.Products([water, bread], "woda").ShouldBe([water]);
        LocalSearch.Products([water, bread], "pie-chleb").ShouldBe([bread]);
        LocalSearch.Products([water, bread], "napoje").ShouldBe([water]);
    }

    private static ClientLookupDto Client(string name, string surname, string email, string phone) =>
        new(Guid.NewGuid(), email.Split('@')[0], email, name, surname, phone, []);
}
