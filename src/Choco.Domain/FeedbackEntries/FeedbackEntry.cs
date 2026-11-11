using Choco.Domain.Primitives.Entities;

namespace Choco.Domain.FeedbackEntries;

public sealed class FeedbackEntry : AggregateRoot, IAuditable
{
    public const int MessageMinLength = 10;
    public const int MessageMaxLength = 2000;
    public const int EnumMaxLength = 32;

    public Guid AuthorId { get; private set; }
    public FeedbackCategory Category { get; private set; }
    public string Message { get; private set; } = null!;
    public FeedbackContext Context { get; private set; } = null!;
    public FeedbackStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ModifiedAt { get; private set; }

    private FeedbackEntry() { }

    private FeedbackEntry(Guid id, Guid authorId, FeedbackCategory category, string message, FeedbackContext context)
        : base(id)
    {
        AuthorId = authorId;
        Category = category;
        Message = message;
        Context = context;
        Status = FeedbackStatus.New;
    }

    public static FeedbackEntry Create(Guid authorId, FeedbackCategory category, string message, FeedbackContext context)
    {
        if (authorId == Guid.Empty)
        {
            throw new ArgumentException("Value cannot be empty.", nameof(authorId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ArgumentNullException.ThrowIfNull(context);

        string trimmedMessage = message.Trim();
        ArgumentOutOfRangeException.ThrowIfLessThan(trimmedMessage.Length, MessageMinLength, nameof(message));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(trimmedMessage.Length, MessageMaxLength, nameof(message));

        return new FeedbackEntry(Guid.NewGuid(), authorId, category, trimmedMessage, context);
    }

    public void ChangeStatus(FeedbackStatus status)
    {
        Status = status;
    }
}
