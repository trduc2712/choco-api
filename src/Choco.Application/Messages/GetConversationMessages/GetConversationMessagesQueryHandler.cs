using Choco.Application.Abstractions.Authentication;
using Choco.Application.Abstractions.Messaging;
using Choco.Contract.Abstractions;
using Choco.Contract.Messages;
using Choco.Domain.Conversations;
using Choco.SharedKernel.Results;

namespace Choco.Application.Messages.GetConversationMessages;

internal sealed class GetConversationMessagesQueryHandler(
    ICurrentUser currentUser,
    IConversationRepository conversations,
    IMessageRepository messages)
    : IQueryHandler<GetConversationMessagesQuery, PageResponse<MessageResponse>>
{
    public async Task<Result<PageResponse<MessageResponse>>> Handle(
        GetConversationMessagesQuery query, CancellationToken cancellationToken)
    {
        Result<Guid> caller = await currentUser.GetIdAsync(cancellationToken);
        if (caller.IsFailure)
        {
            return Result<PageResponse<MessageResponse>>.Failure(caller.Error);
        }

        if (!await conversations.ExistsForParticipantForReadAsync(query.ConversationId, caller.Value, cancellationToken))
        {
            return Result<PageResponse<MessageResponse>>.Failure(ConversationErrors.NotFound);
        }

        MessageCursor.TryDecode(query.Cursor, out MessageCursor? cursor);
        long? before = cursor is { IsCatchUp: false } ? cursor.Sequence : null;
        long? after = cursor is { IsCatchUp: true } ? cursor.Sequence : query.AfterSequence;

        IReadOnlyList<Message> rows = await messages.ListForReadAsync(
            query.ConversationId, before, after, query.Limit + 1, cancellationToken);
        List<MessageResponse> items = rows.Take(query.Limit).Select(message => message.ToResponse()).ToList();
        string? nextCursor = rows.Count > query.Limit
            ? new MessageCursor(items[^1].Sequence, after.HasValue).Encode()
            : null;

        return Result<PageResponse<MessageResponse>>.Success(new PageResponse<MessageResponse>(items, nextCursor));
    }
}
