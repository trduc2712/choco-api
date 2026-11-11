using Choco.Application.Abstractions.Authentication;
using Choco.Application.Abstractions.Messaging;
using Choco.Contract.Users;
using Choco.Domain.UserBlocks;
using Choco.Domain.Users;
using Choco.SharedKernel.Results;

namespace Choco.Application.Users.UnblockUser;

internal sealed class UnblockUserCommandHandler(
    ICurrentUser currentUser,
    IUserRepository userRepository,
    IUserBlockRepository blockRepository) : ICommandHandler<UnblockUserCommand>
{
    public async Task<Result> Handle(UnblockUserCommand command, CancellationToken cancellationToken)
    {
        Result<Guid> caller = await currentUser.GetIdAsync(cancellationToken);
        if (caller.IsFailure)
        {
            return caller;
        }

        User? target = await userRepository.GetByIdForReadAsync(command.UserId, cancellationToken);
        if (target is null)
        {
            return Result.Failure(UserErrors.NotFound);
        }

        await blockRepository.RemoveIfPresentAsync(caller.Value, target.Id, cancellationToken);
        return Result.Success();
    }
}
