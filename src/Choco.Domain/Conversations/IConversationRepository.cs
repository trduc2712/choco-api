using Choco.Domain.Primitives.Persistence;

namespace Choco.Domain.Conversations;

public interface IConversationRepository : IRepository<Conversation>
{
    Task AddIfMissingAsync(Conversation conversation, CancellationToken cancellationToken);
    Task<Conversation?> GetBetweenForUpdateAsync(Guid userId, Guid otherUserId, CancellationToken cancellationToken);
    Task<Conversation?> GetForParticipantForUpdateAsync(Guid id, Guid userId, CancellationToken cancellationToken);
    Task<ConversationEntry?> GetBetweenForReadAsync(Guid userId, Guid otherUserId, CancellationToken cancellationToken);
    Task<bool> ExistsForParticipantForReadAsync(Guid id, Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ConversationEntry>> ListForReadAsync(
        Guid userId, (DateTimeOffset LastActivityAt, Guid Id)? after, int count, CancellationToken cancellationToken);
}
