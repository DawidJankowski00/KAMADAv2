using Kamada.Shared.Contracts.Assistant;
using Kamada.Shared.Matching.Clients;
using Kamada.Shared.Matching.Text;

namespace Kamada.Shared.Matching.UnitTests;

public sealed class ClientMatcherTests
{
    private static readonly ClientRow JanKowalski = new(Guid.NewGuid(), "Jan", "Kowalski", "jan.kowalski@example.com", "+48 600 100 200");
    private static readonly ClientRow AnnaNowak = new(Guid.NewGuid(), "Anna", "Nowak", "anna@example.com", "601 222 333");
    private static readonly ClientRow LukaszZolc = new(Guid.NewGuid(), "Łukasz", "Żółć", "lz@example.com", "602 444 555");
    private static readonly ClientRow[] Clients = [JanKowalski, AnnaNowak, LukaszZolc];

    [Theory]
    [InlineData("600100200")]
    [InlineData("+48 600-100-200")]
    [InlineData("0048600100200")]
    public void Rank_SamePhoneDigits_IsCertainPhoneMatch(string phone)
    {
        var candidates = ClientMatcher.Rank(new ClientMention("Ktoś", "Inny", phone, null), Clients, 3);

        candidates[0].Id.ShouldBe(JanKowalski.Id);
        candidates[0].Score.ShouldBe(1);
        candidates[0].Reason.ShouldBe(ClientMatchReason.Phone);
    }

    [Fact]
    public void Rank_SameEmailIgnoringCase_IsCertainEmailMatch()
    {
        var candidates = ClientMatcher.Rank(new ClientMention(null, null, null, " ANNA@example.com "), Clients, 3);

        candidates.Single().Id.ShouldBe(AnnaNowak.Id);
        candidates[0].Reason.ShouldBe(ClientMatchReason.Email);
    }

    [Theory]
    [InlineData("Lukasz", "Zolc")]
    [InlineData("Żółć", "Łukasz")]
    public void Rank_NameWithoutDiacriticsOrSwapped_FindsClient(string name, string surname)
    {
        var candidates = ClientMatcher.Rank(new ClientMention(name, surname, null, null), Clients, 3);

        candidates[0].Id.ShouldBe(LukaszZolc.Id);
        candidates[0].Score.ShouldBeGreaterThan(0.95);
        candidates[0].Reason.ShouldBe(ClientMatchReason.Name);
    }

    [Fact]
    public void Rank_TypoInSurname_StillRanksClientFirst()
    {
        var candidates = ClientMatcher.Rank(new ClientMention("Jan", "Kowalsky", null, null), Clients, 3);

        candidates[0].Id.ShouldBe(JanKowalski.Id);
        candidates[0].Score.ShouldBeGreaterThan(0.9);
    }

    [Fact]
    public void Rank_UnknownPerson_ReturnsNoCandidates() =>
        ClientMatcher.Rank(new ClientMention("Zbigniew", "Wiśniewski", "700 000 000", null), Clients, 3).ShouldBeEmpty();

    [Fact]
    public void Rank_RespectsLimit()
    {
        ClientRow[] namesakes = [.. Enumerable.Range(0, 5).Select(i => JanKowalski with { Id = Guid.NewGuid(), PhoneNumber = $"60000000{i}" })];

        ClientMatcher.Rank(new ClientMention("Jan", "Kowalski", null, null), namesakes, 3).Count.ShouldBe(3);
    }

    [Fact]
    public void NameScore_OnlyFirstName_CountsLessThanFullName()
    {
        var firstNameOnly = ClientMatcher.NameScore(new ClientMention("Jan", null, null, null), JanKowalski);
        var fullName = ClientMatcher.NameScore(new ClientMention("Jan", "Kowalski", null, null), JanKowalski);

        firstNameOnly.ShouldBeLessThan(fullName);
        firstNameOnly.ShouldBeLessThan(0.9);
    }
}
