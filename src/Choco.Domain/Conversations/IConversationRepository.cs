using Choco.Domain.Primitives.Persistence;

namespace Choco.Domain.Conversations;

public interface IConversationRepository : IRepository<Conversation>
{
    Task AddIfMissingAsync(Conversation conversation, CancellationToken cancellationToken);
    Task<Conversation?> GetBetweenForUpdateAsync(Guid userId, Guid otherUserId, CancellationToken cancellationToken);
}
