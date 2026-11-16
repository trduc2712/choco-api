using Choco.Domain.Conversations;
using Choco.Domain.UserBlocks;
using Choco.Domain.Users;

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

    public Task<Conversation?> GetForParticipantForUpdateAsync(
        Guid id, Guid userId, CancellationToken cancellationToken)
    {
        return DbSet.FromSql($"""
                SELECT * FROM conversations
                WHERE id = {id} AND (first_user_id = {userId} OR second_user_id = {userId})
                FOR UPDATE
                """)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public Task<bool> ExistsForParticipantForReadAsync(Guid id, Guid userId, CancellationToken cancellationToken)
    {
        return ForParticipant(userId).AnyAsync(conversation => conversation.Id == id, cancellationToken);
    }

    public Task<ConversationEntry?> GetBetweenForReadAsync(
        Guid userId, Guid otherUserId, CancellationToken cancellationToken)
    {
        (Guid firstUserId, Guid secondUserId) = Conversation.OrderParticipants(userId, otherUserId);
        IQueryable<Conversation> conversations = DbSet.AsNoTracking()
            .Where(conversation => conversation.FirstUserId == firstUserId && conversation.SecondUserId == secondUserId);

        return WithDetails(conversations, userId).SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ConversationEntry>> ListForReadAsync(
        Guid userId, (DateTimeOffset LastActivityAt, Guid Id)? after, int count, CancellationToken cancellationToken)
    {
        IQueryable<Conversation> conversations = ForParticipant(userId);
        if (after is { } position)
        {
            conversations = conversations.Where(conversation => EF.Functions.LessThan(
                ValueTuple.Create(conversation.LastActivityAt, conversation.Id),
                ValueTuple.Create(position.LastActivityAt, position.Id)));
        }

        return await WithDetails(conversations, userId)
            .OrderByDescending(entry => entry.Conversation.LastActivityAt)
            .ThenByDescending(entry => entry.Conversation.Id)
            .Take(count)
            .ToListAsync(cancellationToken);
    }

    private IQueryable<Conversation> ForParticipant(Guid userId)
    {
        return DbSet.AsNoTracking()
            .Where(conversation => conversation.FirstUserId == userId || conversation.SecondUserId == userId);
    }

    private IQueryable<ConversationEntry> WithDetails(IQueryable<Conversation> conversations, Guid userId)
    {
        return from conversation in conversations
               join user in DbContext.Set<User>().AsNoTracking()
                   on (conversation.FirstUserId == userId ? conversation.SecondUserId : conversation.FirstUserId) equals user.Id
               join message in DbContext.Set<Message>().AsNoTracking()
                   on new { ConversationId = conversation.Id, Sequence = conversation.LastMessageSequence }
                   equals new { message.ConversationId, message.Sequence } into messages
               from lastMessage in messages.DefaultIfEmpty()
               select new ConversationEntry
               {
                   Conversation = conversation,
                   OtherUser = user,
                   LastMessage = lastMessage,
                   IsBlockedByMe = DbContext.Set<UserBlock>().Any(
                       block => block.BlockerId == userId && block.BlockedUserId == user.Id)
               };
    }
}
