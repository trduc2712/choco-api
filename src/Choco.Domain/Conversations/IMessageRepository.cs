namespace Choco.Domain.Conversations;

public interface IMessageRepository
{
    void Add(Message message);

    Task<Message?> GetByClientMessageIdForReadAsync(
        Guid conversationId, Guid senderId, Guid clientMessageId, CancellationToken cancellationToken);

    Task<Message?> GetVisibleToForReadAsync(Guid id, Guid userId, CancellationToken cancellationToken);
}
