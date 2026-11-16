using Choco.Application.Abstractions.Authentication;
using Choco.Application.Abstractions.Messaging;
using Choco.Contract.Abstractions;
using Choco.Contract.Messages;
using Choco.Domain.Conversations;
using Choco.SharedKernel.Results;

namespace Choco.Application.Messages.GetConversations;

internal sealed class GetConversationsQueryHandler(ICurrentUser currentUser, IConversationRepository repository)
    : IQueryHandler<GetConversationsQuery, PageResponse<ConversationResponse>>
{
    public async Task<Result<PageResponse<ConversationResponse>>> Handle(
        GetConversationsQuery query, CancellationToken cancellationToken)
    {
        Result<Guid> caller = await currentUser.GetIdAsync(cancellationToken);
        if (caller.IsFailure)
        {
            return Result<PageResponse<ConversationResponse>>.Failure(caller.Error);
        }

        (DateTimeOffset LastActivityAt, Guid Id)? after =
            ConversationCursor.TryDecode(query.Cursor, out ConversationCursor? cursor)
                ? (cursor!.LastActivityAt, cursor.ConversationId)
                : null;

        IReadOnlyList<ConversationEntry> entries = await repository.ListForReadAsync(
            caller.Value, after, query.Limit + 1, cancellationToken);
        List<ConversationResponse> items = entries.Take(query.Limit)
            .Select(entry => entry.ToResponse(caller.Value))
            .ToList();

        string? nextCursor = null;
        if (entries.Count > query.Limit)
        {
            Conversation last = entries[query.Limit - 1].Conversation;
            nextCursor = new ConversationCursor(last.LastActivityAt, last.Id).Encode();
        }

        return Result<PageResponse<ConversationResponse>>.Success(new PageResponse<ConversationResponse>(items, nextCursor));
    }
}
