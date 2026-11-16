using Choco.Application.Abstractions.Authentication;
using Choco.Application.Abstractions.Messaging;
using Choco.Contract.Messages;
using Choco.Domain.Conversations;
using Choco.Domain.Primitives.Persistence;
using Choco.SharedKernel.Results;

namespace Choco.Application.Messages.MarkConversationRead;

internal sealed class MarkConversationReadCommandHandler(
    ICurrentUser currentUser,
    IConversationRepository repository,
    IUnitOfWork unitOfWork)
    : ICommandHandler<MarkConversationReadCommand>
{
    public async Task<Result> Handle(MarkConversationReadCommand command, CancellationToken cancellationToken)
    {
        Result<Guid> caller = await currentUser.GetIdAsync(cancellationToken);
        if (caller.IsFailure)
        {
            return caller;
        }

        return await unitOfWork.InTransactionAsync(async () =>
        {
            Conversation? conversation = await repository.GetForParticipantForUpdateAsync(
                command.ConversationId, caller.Value, cancellationToken);
            if (conversation is null)
            {
                return Result.Failure(ConversationErrors.NotFound);
            }

            conversation.MarkRead(caller.Value, command.Sequence);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }, cancellationToken);
    }
}
