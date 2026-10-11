using Kamada.Shared.Matching.Clients;

namespace Kamada.Shared.Matching.UnitTests;

public sealed class UsernameSuggestionTests
{
    [Theory]
    [InlineData("Jan", "Kowalski", "jan.kowalski")]
    [InlineData("Łucja", "Nowak-Wiśniewska", "lucja.nowak-wisniewska")]
    [InlineData(" Anna ", null, "anna")]
    [InlineData(null, "Żak", "zak")]
    [InlineData(null, null, UsernameSuggestion.Fallback)]
    [InlineData("Al", null, UsernameSuggestion.Fallback)]
    public void Base_FoldsToAscii(string? name, string? surname, string expected) =>
        UsernameSuggestion.Base(name, surname).ShouldBe(expected);

    [Fact]
    public void Base_LongName_LeavesRoomForSuffix() =>
        UsernameSuggestion.Base(new string('a', 40), new string('b', 40)).Length.ShouldBeLessThanOrEqualTo(46);

    [Fact]
    public void FirstFree_BaseTaken_AppendsLowestFreeNumber() =>
        UsernameSuggestion.FirstFree("jan.kowalski", new HashSet<string> { "jan.kowalski", "jan.kowalski2" }).ShouldBe("jan.kowalski3");

    [Fact]
    public void FirstFree_BaseFree_ReturnsBase() =>
        UsernameSuggestion.FirstFree("jan.kowalski", new HashSet<string> { "jan.kowalski2" }).ShouldBe("jan.kowalski");
}
