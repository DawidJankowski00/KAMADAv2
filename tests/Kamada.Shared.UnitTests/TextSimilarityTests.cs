using Kamada.Shared.Kernel.Text;

namespace Kamada.Shared.UnitTests;

public sealed class TextSimilarityTests
{
    [Theory]
    [InlineData("Łódź, ul. Piotrkowska 1/2", "lodz ul piotrkowska 1 2")]
    [InlineData("  ŻÓŁTA   Gęś ", "zolta ges")]
    [InlineData("Woda 1,5 l", "woda 1 5 l")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void Fold_RemovesDiacriticsCaseAndPunctuation(string? input, string expected) =>
        TextSimilarity.Fold(input).ShouldBe(expected);

    [Fact]
    public void Compact_IgnoresSpacesAndCase() =>
        TextSimilarity.Compact("15 A").ShouldBe(TextSimilarity.Compact("15a"));

    [Fact]
    public void JaroWinkler_KnownPair_MatchesReferenceValue() =>
        TextSimilarity.JaroWinkler("martha", "marhta").ShouldBe(0.961, tolerance: 0.001);

    [Theory]
    [InlineData("", "", 1)]
    [InlineData("abc", "", 0)]
    [InlineData("abc", "xyz", 0)]
    public void JaroWinkler_EdgeCases(string first, string second, double expected) =>
        TextSimilarity.JaroWinkler(first, second).ShouldBe(expected);

    [Fact]
    public void Similarity_InflectedPolishName_IsHigh() =>
        TextSimilarity.Similarity("Kowalskiego", "Kowalski").ShouldBeGreaterThan(0.9);

    [Fact]
    public void PhraseScore_InflectedPhrase_PrefersTheRightProduct()
    {
        var still = TextSimilarity.PhraseScore("wód niegazowanych 1,5 l", "Woda niegazowana 1,5 l");
        var sparkling = TextSimilarity.PhraseScore("wód niegazowanych 1,5 l", "Woda gazowana 1,5 l");

        still.ShouldBeGreaterThan(0.85);
        still.ShouldBeGreaterThan(sparkling);
    }

    [Fact]
    public void PhraseScore_NumbersMustBeEqual()
    {
        var right = TextSimilarity.PhraseScore("woda 1,5 l", "Woda 1,5 l");
        var wrong = TextSimilarity.PhraseScore("woda 1,5 l", "Woda 0,5 l");

        right.ShouldBe(1);
        wrong.ShouldBeLessThan(right);
    }

    [Fact]
    public void PhraseScore_ExtraCandidateWords_LowerTheScoreSlightly()
    {
        var exact = TextSimilarity.PhraseScore("chleb razowy", "Chleb razowy");
        var longer = TextSimilarity.PhraseScore("chleb razowy", "Chleb razowy z ziarnami słonecznika");

        exact.ShouldBe(1);
        longer.ShouldBeLessThan(exact);
        longer.ShouldBeGreaterThan(0.85);
    }

    [Fact]
    public void PhraseScore_EmptyInput_IsZero() =>
        TextSimilarity.PhraseScore(" ", "Chleb").ShouldBe(0);
}
