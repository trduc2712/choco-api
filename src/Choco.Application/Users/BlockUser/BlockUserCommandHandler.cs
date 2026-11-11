using Choco.Application.Abstractions.Authentication;
using Choco.Application.Abstractions.Messaging;
using Choco.Contract.Users;
using Choco.Domain.Primitives.Persistence;
using Choco.Domain.UserBlocks;
using Choco.Domain.UserFollows;
using Choco.Domain.Users;
using Choco.SharedKernel.Results;

namespace Choco.Application.Users.BlockUser;

internal sealed class BlockUserCommandHandler(
    ICurrentUser currentUser,
    IUserRepository userRepository,
    IUserBlockRepository blockRepository,
    IUserFollowRepository followRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<BlockUserCommand>
{
    public async Task<Result> Handle(BlockUserCommand command, CancellationToken cancellationToken)
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

        Result<UserBlock> block = UserBlock.Create(caller.Value, target.Id, timeProvider.GetUtcNow());
        if (block.IsFailure)
        {
            return block;
        }

        await unitOfWork.InTransactionAsync(async () =>
        {
            if (await blockRepository.AddIfMissingAsync(block.Value, cancellationToken))
            {
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }

            await followRepository.RemoveIfPresentAsync(caller.Value, target.Id, cancellationToken);
            await followRepository.RemoveIfPresentAsync(target.Id, caller.Value, cancellationToken);

            return true;
        }, cancellationToken);

        return Result.Success();
    }
}
