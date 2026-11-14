using Choco.Application.Feedback;
using Choco.Contract.Feedback;
using Choco.Domain.FeedbackEntries;

namespace Choco.Application.UnitTests.Feedback;

public sealed class FeedbackWireTests
{
    [Theory]
    [InlineData(FeedbackStatus.New, "new")]
    [InlineData(FeedbackStatus.InProgress, "in_progress")]
    [InlineData(FeedbackStatus.Resolved, "resolved")]
    [InlineData(FeedbackStatus.WontFix, "wont_fix")]
    public void ToWire_WithStatus_ReturnsLowerSnakeCase(FeedbackStatus status, string expected)
    {
        FeedbackWire.ToWire(status).Should().Be(expected);
    }

    [Theory]
    [InlineData("new", true, FeedbackStatus.New)]
    [InlineData("in_progress", true, FeedbackStatus.InProgress)]
    [InlineData("wont_fix", true, FeedbackStatus.WontFix)]
    [InlineData("InProgress", false, default(FeedbackStatus))]
    [InlineData("In_Progress", false, default(FeedbackStatus))]
    [InlineData("", false, default(FeedbackStatus))]
    [InlineData(null, false, default(FeedbackStatus))]
    public void TryParse_WithText_ParsesOnlyExactWireValues(string? text, bool expectedParsed, FeedbackStatus expected)
    {
        bool parsed = FeedbackWire.TryParse(text, out FeedbackStatus status);

        parsed.Should().Be(expectedParsed);
        status.Should().Be(expected);
    }

    [Fact]
    public void Options_ForCategory_ListsWireValuesInOrder()
    {
        FeedbackWire.Options<FeedbackCategory>().Should().Be("bug, suggestion, other");
    }

    [Fact]
    public void WireValues_InTheContract_MatchTheDomainEnums()
    {
        FeedbackWireValues.Categories.Should().BeEquivalentTo(Enum.GetValues<FeedbackCategory>().Select(FeedbackWire.ToWire));
        FeedbackWireValues.Statuses.Should().BeEquivalentTo(Enum.GetValues<FeedbackStatus>().Select(FeedbackWire.ToWire));
        FeedbackWireValues.Locales.Should().BeEquivalentTo(Enum.GetValues<FeedbackLocale>().Select(FeedbackWire.ToWire));
        FeedbackWireValues.Themes.Should().BeEquivalentTo(Enum.GetValues<FeedbackTheme>().Select(FeedbackWire.ToWire));
    }
}
