using System.Reflection;
using Kamada.Shared.Contracts.Assistant;
using Kamada.Web.Shared.Api;

namespace Kamada.Web.Shared.UnitTests;

public sealed class AssistantLabelsTests
{
    public static TheoryData<string> WarningCodes => new(typeof(DraftWarningCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Select(f => (string)f.GetValue(null)!));

    [Theory]
    [MemberData(nameof(WarningCodes))]
    public void Of_EveryDraftWarningCode_HasPolishMessage(string code) =>
        Labels.Of(new DraftWarningDto(code, "x")).ShouldNotBe(code);

    [Fact]
    public void Of_WarningWithSubject_QuotesTheSpokenPhrase() =>
        Labels.Of(new DraftWarningDto(DraftWarningCodes.ProductNotFound, "pralka")).ShouldBe("Nie znaleziono produktu „pralka”.");

    [Fact]
    public void Of_UnknownWarningCode_FallsBackToTheCode() =>
        Labels.Of(new DraftWarningDto("Something.New")).ShouldBe("Something.New");

    [Theory]
    [InlineData(1, "100 %")]
    [InlineData(0.874, "87 %")]
    [InlineData(0.875, "88 %")]
    public void Score_IsAPercentage(double score, string expected) =>
        Labels.Score(score).ShouldBe(expected);

    [Theory]
    [InlineData("Assistant.Disabled")]
    [InlineData("Assistant.ModelUnavailable")]
    [InlineData("Assistant.ExtractionFailed")]
    [InlineData("Assistant.NothingRecognized")]
    public void TryTranslate_AssistantCodes_HavePolishMessages(string code) =>
        ErrorMessages.TryTranslate(code).ShouldNotBeNullOrWhiteSpace();
}
