using Choco.Application.Abstractions.Authentication;
using Choco.Application.Abstractions.Messaging;
using Choco.Contract.Messages;
using Choco.Domain.Conversations;
using Choco.SharedKernel.Results;

namespace Choco.Application.Messages.GetConversationByUser;

internal sealed class GetConversationByUserQueryHandler(ICurrentUser currentUser, IConversationRepository repository)
    : IQueryHandler<GetConversationByUserQuery, ConversationResponse>
{
    public async Task<Result<ConversationResponse>> Handle(GetConversationByUserQuery query, CancellationToken cancellationToken)
    {
        Result<Guid> caller = await currentUser.GetIdAsync(cancellationToken);
        if (caller.IsFailure)
        {
            return Result<ConversationResponse>.Failure(caller.Error);
        }

        ConversationEntry? entry = await repository.GetBetweenForReadAsync(caller.Value, query.UserId, cancellationToken);
        return entry is null
            ? Result<ConversationResponse>.Failure(ConversationErrors.NotFound)
            : Result<ConversationResponse>.Success(entry.ToResponse(caller.Value));
    }
}
