using Choco.Domain.Primitives.Entities;

namespace Choco.Domain.Conversations;

public sealed class Message : BaseEntity
{
    public const int ContentMaxLength = 2000;

    public Guid ConversationId { get; private set; }
    public long Sequence { get; private set; }
    public Guid SenderId { get; private set; }
    public Guid ClientMessageId { get; private set; }
    public string Content { get; private set; } = null!;
    public DateTimeOffset SentAt { get; private set; }

    private Message() { }

    private Message(
        Guid id,
        Guid conversationId,
        long sequence,
        Guid senderId,
        Guid clientMessageId,
        string content,
        DateTimeOffset sentAt)
        : base(id)
    {
        ConversationId = conversationId;
        Sequence = sequence;
        SenderId = senderId;
        ClientMessageId = clientMessageId;
        Content = content;
        SentAt = sentAt;
    }

    internal static Message Create(
        Guid conversationId,
        long sequence,
        Guid senderId,
        Guid clientMessageId,
        string content,
        DateTimeOffset now)
    {
        if (clientMessageId == Guid.Empty)
        {
            throw new ArgumentException("Value cannot be empty.", nameof(clientMessageId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        string trimmedContent = content.Trim();
        ArgumentOutOfRangeException.ThrowIfGreaterThan(trimmedContent.Length, ContentMaxLength, nameof(content));

        return new Message(Guid.NewGuid(), conversationId, sequence, senderId, clientMessageId, trimmedContent, now);
    }
}
