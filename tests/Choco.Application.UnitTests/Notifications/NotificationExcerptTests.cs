using Choco.Application.Notifications;

namespace Choco.Application.UnitTests.Notifications;

public sealed class NotificationExcerptTests
{
    [Fact]
    public void Of_WithoutContent_ReturnsNull()
    {
        string? excerpt = NotificationExcerpt.Of(null);

        excerpt.Should().BeNull();
    }

    [Fact]
    public void Of_WithShortContent_ReturnsItUnchanged()
    {
        string? excerpt = NotificationExcerpt.Of("content");

        excerpt.Should().Be("content");
    }

    [Fact]
    public void Of_WithContentAtMaxLength_ReturnsItUnchanged()
    {
        string content = new('c', NotificationExcerpt.MaxLength);

        string? excerpt = NotificationExcerpt.Of(content);

        excerpt.Should().Be(content);
    }

    [Fact]
    public void Of_WithContentOverMaxLength_ReturnsAnExcerptOfMaxLengthEndingWithEllipsis()
    {
        string content = new('c', NotificationExcerpt.MaxLength + 1);

        string? excerpt = NotificationExcerpt.Of(content);

        excerpt.Should().HaveLength(NotificationExcerpt.MaxLength);
        excerpt.Should().EndWith("…");
        excerpt.Should().StartWith("ccc");
    }

    [Fact]
    public void Of_WithWhitespaceRuns_CollapsesThemIntoSingleSpaces()
    {
        string? excerpt = NotificationExcerpt.Of("  line-1\n\n  line-2\t line-3 \n");

        excerpt.Should().Be("line-1 line-2 line-3");
    }

    [Fact]
    public void Of_WhenCutFallsBetweenTrailingSpaces_DropsTheSpacesBeforeTheEllipsis()
    {
        string content = new string('c', NotificationExcerpt.MaxLength - 2) + " " + new string('d', 10);

        string? excerpt = NotificationExcerpt.Of(content);

        excerpt.Should().Be(new string('c', NotificationExcerpt.MaxLength - 2) + "…");
    }

    [Fact]
    public void Of_WhenCutFallsInsideASurrogatePair_DoesNotSplitIt()
    {
        string content = new string('c', NotificationExcerpt.MaxLength - 2) + "\U0001F600" + new string('d', 10);

        string? excerpt = NotificationExcerpt.Of(content);

        excerpt.Should().Be(new string('c', NotificationExcerpt.MaxLength - 2) + "…");
    }
}
