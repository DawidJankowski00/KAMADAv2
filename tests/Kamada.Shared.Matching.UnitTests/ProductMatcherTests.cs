using Kamada.Shared.Matching.Products;

namespace Kamada.Shared.Matching.UnitTests;

public sealed class ProductMatcherTests
{
    private static readonly ProductRow Still = new(Guid.NewGuid(), "Woda niegazowana 1,5 l", "WODA-NG-15", 2.49m, 120);
    private static readonly ProductRow Sparkling = new(Guid.NewGuid(), "Woda gazowana 1,5 l", "WODA-G-15", 2.49m, 80);
    private static readonly ProductRow Bread = new(Guid.NewGuid(), "Chleb razowy", "CHLEB-RAZ", 6.99m, 15);
    private static readonly ProductRow[] Products = [Still, Sparkling, Bread];

    [Fact]
    public void Rank_BaseFormName_PutsExactProductFirst()
    {
        var candidates = ProductMatcher.Rank("woda niegazowana 1,5 l", Products, 3);

        candidates[0].Id.ShouldBe(Still.Id);
        candidates[0].Score.ShouldBe(1);
        candidates.ShouldNotContain(c => c.Id == Bread.Id);
    }

    [Fact]
    public void Rank_Sku_IsExactMatch()
    {
        var candidates = ProductMatcher.Rank("chleb-raz", Products, 3);

        candidates[0].Id.ShouldBe(Bread.Id);
        candidates[0].Score.ShouldBe(1);
    }

    [Fact]
    public void Rank_GenericWord_ReturnsBothVariantsWithEqualScore()
    {
        var candidates = ProductMatcher.Rank("woda", Products, 3);

        candidates.Select(c => c.Id).ShouldBe([Sparkling.Id, Still.Id], ignoreOrder: true);
        candidates[0].Score.ShouldBe(candidates[1].Score);
    }

    [Fact]
    public void Rank_VariantWord_SeparatesStillFromSparklingByAClearMargin()
    {
        ProductRow[] products =
        [
            new(Guid.NewGuid(), "Woda mineralna niegazowana 1,5 l", "SP-NAP-001", 2.49m, 240),
            new(Guid.NewGuid(), "Woda mineralna gazowana 1,5 l", "SP-NAP-002", 2.49m, 180),
        ];

        var candidates = ProductMatcher.Rank("woda niegazowana 1,5 l", products, 3);

        candidates[0].Sku.ShouldBe("SP-NAP-001");
        candidates[0].Score.ShouldBeGreaterThanOrEqualTo(0.9);
        (candidates[0].Score - candidates[1].Score).ShouldBeGreaterThan(0.05);
    }

    [Fact]
    public void Rank_PhraseWrittenAsTwoWords_MatchesOneWordName()
    {
        ProductRow powerbank = new(Guid.NewGuid(), "Powerbank 20 000 mAh", "EL-SMA-002", 159m, 45);

        ProductMatcher.Rank("power bank", [powerbank, .. Products], 3)[0].Id.ShouldBe(powerbank.Id);
    }

    [Fact]
    public void Rank_UnrelatedPhrase_ReturnsNothing() =>
        ProductMatcher.Rank("pralka automatyczna", Products, 3).ShouldBeEmpty();
}
