using Choco.Domain.FeedbackEntries;

namespace Choco.Domain.UnitTests.FeedbackEntries;

public sealed class FeedbackEntryTests
{
    private static readonly Guid AuthorId = new("00000000-0000-0000-0000-000000000001");
    private static readonly FeedbackContext Context = new("/path", FeedbackLocale.Vi, FeedbackTheme.Dark, "user-agent", 1280, 720, "app-version");

    [Fact]
    public void Create_WithValidValues_CreatesNewFeedbackWithTrimmedMessage()
    {
        FeedbackEntry entry = FeedbackEntry.Create(AuthorId, FeedbackCategory.Suggestion, "  feedback message  ", Context);

        entry.Id.Should().NotBeEmpty();
        entry.AuthorId.Should().Be(AuthorId);
        entry.Category.Should().Be(FeedbackCategory.Suggestion);
        entry.Message.Should().Be("feedback message");
        entry.Context.Should().Be(Context);
        entry.Status.Should().Be(FeedbackStatus.New);
    }

    [Fact]
    public void Create_WithMessageAtLimits_Succeeds()
    {
        FeedbackEntry.Create(AuthorId, FeedbackCategory.Bug, new string('m', FeedbackEntry.MessageMinLength), Context)
            .Message.Should().HaveLength(FeedbackEntry.MessageMinLength);
        FeedbackEntry.Create(AuthorId, FeedbackCategory.Bug, new string('m', FeedbackEntry.MessageMaxLength), Context)
            .Message.Should().HaveLength(FeedbackEntry.MessageMaxLength);
    }

    [Fact]
    public void Create_WithEmptyAuthorId_ThrowsArgumentException()
    {
        Func<FeedbackEntry> act = () => FeedbackEntry.Create(Guid.Empty, FeedbackCategory.Bug, "feedback message", Context);

        act.Should().Throw<ArgumentException>().WithParameterName("authorId");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Create_WithBlankMessage_ThrowsArgumentException(string? message)
    {
        Func<FeedbackEntry> act = () => FeedbackEntry.Create(AuthorId, FeedbackCategory.Bug, message!, Context);

        act.Should().Throw<ArgumentException>().WithParameterName("message");
    }

    [Fact]
    public void Create_WithMessageUnderLimitAfterTrimming_ThrowsArgumentOutOfRangeException()
    {
        string message = $" {new string('m', FeedbackEntry.MessageMinLength - 1)} ";

        Func<FeedbackEntry> act = () => FeedbackEntry.Create(AuthorId, FeedbackCategory.Bug, message, Context);

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("message");
    }

    [Fact]
    public void Create_WithMessageOverLimit_ThrowsArgumentOutOfRangeException()
    {
        string message = new('m', FeedbackEntry.MessageMaxLength + 1);

        Func<FeedbackEntry> act = () => FeedbackEntry.Create(AuthorId, FeedbackCategory.Bug, message, Context);

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("message");
    }

    [Fact]
    public void Create_WithNullContext_ThrowsArgumentNullException()
    {
        Func<FeedbackEntry> act = () => FeedbackEntry.Create(AuthorId, FeedbackCategory.Bug, "feedback message", null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("context");
    }

    [Theory]
    [InlineData(FeedbackStatus.InProgress)]
    [InlineData(FeedbackStatus.Resolved)]
    [InlineData(FeedbackStatus.WontFix)]
    [InlineData(FeedbackStatus.New)]
    public void ChangeStatus_WithStatus_SetsStatusAndKeepsTheRest(FeedbackStatus status)
    {
        FeedbackEntry entry = FeedbackEntry.Create(AuthorId, FeedbackCategory.Bug, "feedback message", Context);

        entry.ChangeStatus(status);

        entry.Status.Should().Be(status);
        entry.Message.Should().Be("feedback message");
        entry.AuthorId.Should().Be(AuthorId);
    }
}
