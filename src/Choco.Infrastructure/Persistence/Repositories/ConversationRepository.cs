using Choco.Domain.Conversations;

using Microsoft.EntityFrameworkCore;

namespace Choco.Infrastructure.Persistence.Repositories;

internal sealed class ConversationRepository : Repository<Conversation>, IConversationRepository
{
    public ConversationRepository(ChocoDbContext dbContext) : base(dbContext) { }

    public async Task AddIfMissingAsync(Conversation conversation, CancellationToken cancellationToken)
    {
        await DbContext.Database.ExecuteSqlAsync($"""
            INSERT INTO conversations (
                id,
                first_user_id,
                second_user_id,
                first_user_last_read_sequence,
                second_user_last_read_sequence,
                last_message_sequence,
                last_activity_at)
            VALUES (
                {conversation.Id},
                {conversation.FirstUserId},
                {conversation.SecondUserId},
                {conversation.FirstUserLastReadSequence},
                {conversation.SecondUserLastReadSequence},
                {conversation.LastMessageSequence},
                {conversation.LastActivityAt})
            ON CONFLICT (first_user_id, second_user_id) DO NOTHING
            """, cancellationToken);
    }

    public Task<Conversation?> GetBetweenForUpdateAsync(
        Guid userId, Guid otherUserId, CancellationToken cancellationToken)
    {
        (Guid firstUserId, Guid secondUserId) = Conversation.OrderParticipants(userId, otherUserId);

        return DbSet.FromSql($"""
                SELECT * FROM conversations
                WHERE first_user_id = {firstUserId} AND second_user_id = {secondUserId}
                FOR UPDATE
                """)
            .SingleOrDefaultAsync(cancellationToken);
    }
}
