using Choco.Domain.Conversations;

using Microsoft.EntityFrameworkCore;

namespace Choco.Infrastructure.Persistence.Repositories;

internal sealed class MessageRepository : IMessageRepository
{
    private readonly ChocoDbContext _dbContext;

    public MessageRepository(ChocoDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public void Add(Message message)
    {
        _dbContext.Set<Message>().Add(message);
    }

    public Task<Message?> GetByClientMessageIdForReadAsync(
        Guid conversationId, Guid senderId, Guid clientMessageId, CancellationToken cancellationToken)
    {
        return _dbContext.Set<Message>().AsNoTracking().SingleOrDefaultAsync(
            message => message.ConversationId == conversationId
                && message.SenderId == senderId
                && message.ClientMessageId == clientMessageId,
            cancellationToken);
    }

    public Task<Message?> GetVisibleToForReadAsync(Guid id, Guid userId, CancellationToken cancellationToken)
    {
        return _dbContext.Set<Message>().AsNoTracking()
            .Where(message => message.Id == id)
            .Where(message => _dbContext.Set<Conversation>().Any(conversation =>
                conversation.Id == message.ConversationId
                && (conversation.FirstUserId == userId || conversation.SecondUserId == userId)))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Message>> ListForReadAsync(
        Guid conversationId, long? beforeSequence, long? afterSequence, int count, CancellationToken cancellationToken)
    {
        IQueryable<Message> messages = _dbContext.Set<Message>().AsNoTracking()
            .Where(message => message.ConversationId == conversationId);

        if (afterSequence is long after)
        {
            return await messages.Where(message => message.Sequence > after)
                .OrderBy(message => message.Sequence)
                .Take(count)
                .ToListAsync(cancellationToken);
        }

        if (beforeSequence is long before)
        {
            messages = messages.Where(message => message.Sequence < before);
        }

        return await messages.OrderByDescending(message => message.Sequence)
            .Take(count)
            .ToListAsync(cancellationToken);
    }
}
