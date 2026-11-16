using Choco.Application.Abstractions.Authentication;
using Choco.Application.Abstractions.Messaging;
using Choco.Contract.Messages;
using Choco.Domain.Conversations;
using Choco.SharedKernel.Results;

namespace Choco.Application.Messages.GetMessage;

internal sealed class GetMessageQueryHandler(ICurrentUser currentUser, IMessageRepository messageRepository)
    : IQueryHandler<GetMessageQuery, MessageResponse>
{
    public async Task<Result<MessageResponse>> Handle(GetMessageQuery query, CancellationToken cancellationToken)
    {
        Result<Guid> caller = await currentUser.GetIdAsync(cancellationToken);
        if (caller.IsFailure)
        {
            return Result<MessageResponse>.Failure(caller.Error);
        }

        Message? message = await messageRepository.GetVisibleToForReadAsync(query.Id, caller.Value, cancellationToken);

        return message is null
            ? Result<MessageResponse>.Failure(MessageErrors.NotFound)
            : Result<MessageResponse>.Success(message.ToResponse());
    }
}
