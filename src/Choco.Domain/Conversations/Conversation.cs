using System.Runtime.CompilerServices;

using Choco.Domain.Primitives.Entities;
using Choco.SharedKernel.Results;

namespace Choco.Domain.Conversations;

public sealed class Conversation : AggregateRoot
{
    public Guid FirstUserId { get; private set; }
    public Guid SecondUserId { get; private set; }
    public long FirstUserLastReadSequence { get; private set; }
    public long SecondUserLastReadSequence { get; private set; }
    public long LastMessageSequence { get; private set; }
    public DateTimeOffset LastActivityAt { get; private set; }

    private Conversation() { }

    private Conversation(Guid id, Guid firstUserId, Guid secondUserId, DateTimeOffset now)
        : base(id)
    {
        FirstUserId = firstUserId;
        SecondUserId = secondUserId;
        LastActivityAt = now;
    }

    public static Result<Conversation> Create(Guid userId, Guid otherUserId, DateTimeOffset now)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("Value cannot be empty.", nameof(userId));
        }

        if (otherUserId == Guid.Empty)
        {
            throw new ArgumentException("Value cannot be empty.", nameof(otherUserId));
        }

        if (userId == otherUserId)
        {
            return Result<Conversation>.Failure(ConversationErrors.SelfConversation);
        }

        (Guid firstUserId, Guid secondUserId) = OrderParticipants(userId, otherUserId);

        return Result<Conversation>.Success(new Conversation(Guid.NewGuid(), firstUserId, secondUserId, now));
    }

    public static (Guid FirstUserId, Guid SecondUserId) OrderParticipants(Guid userId, Guid otherUserId)
    {
        return userId.CompareTo(otherUserId) < 0 ? (userId, otherUserId) : (otherUserId, userId);
    }

    public bool IsParticipant(Guid userId)
    {
        return userId == FirstUserId || userId == SecondUserId;
    }

    public Message SendMessage(Guid senderId, Guid clientMessageId, string content, DateTimeOffset now)
    {
        EnsureParticipant(senderId);

        Message message = Message.Create(Id, LastMessageSequence + 1, senderId, clientMessageId, content, now);

        LastMessageSequence = message.Sequence;
        LastActivityAt = now;
        SetLastReadSequence(senderId, message.Sequence);

        return message;
    }

    public void MarkRead(Guid userId, long sequence)
    {
        EnsureParticipant(userId);
        ArgumentOutOfRangeException.ThrowIfNegative(sequence);

        long readSequence = Math.Min(sequence, LastMessageSequence);

        if (readSequence > LastReadSequenceOf(userId))
        {
            SetLastReadSequence(userId, readSequence);
        }
    }

    public long LastReadSequenceOf(Guid userId)
    {
        EnsureParticipant(userId);

        return userId == FirstUserId ? FirstUserLastReadSequence : SecondUserLastReadSequence;
    }

    public long UnreadCountFor(Guid userId)
    {
        return LastMessageSequence - LastReadSequenceOf(userId);
    }

    private void SetLastReadSequence(Guid userId, long sequence)
    {
        if (userId == FirstUserId)
        {
            FirstUserLastReadSequence = sequence;
        }
        else
        {
            SecondUserLastReadSequence = sequence;
        }
    }

    private void EnsureParticipant(Guid userId, [CallerArgumentExpression(nameof(userId))] string? paramName = null)
    {
        if (!IsParticipant(userId))
        {
            throw new ArgumentException("The user does not take part in the conversation.", paramName);
        }
    }
}
