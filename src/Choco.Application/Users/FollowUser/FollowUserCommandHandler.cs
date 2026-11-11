using Choco.Application.Abstractions.Authentication;
using Choco.Application.Abstractions.Messaging;
using Choco.Contract.Users;
using Choco.Domain.Primitives.Persistence;
using Choco.Domain.UserBlocks;
using Choco.Domain.UserFollows;
using Choco.Domain.Users;
using Choco.SharedKernel.Results;

namespace Choco.Application.Users.FollowUser;

internal sealed class FollowUserCommandHandler : ICommandHandler<FollowUserCommand>
{
    private readonly ICurrentUser _currentUser;
    private readonly IUserRepository _userRepository;
    private readonly IUserFollowRepository _followRepository;
    private readonly IUserBlockRepository _blockRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public FollowUserCommandHandler(
        ICurrentUser currentUser,
        IUserRepository userRepository,
        IUserFollowRepository followRepository,
        IUserBlockRepository blockRepository,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider)
    {
        _currentUser = currentUser;
        _userRepository = userRepository;
        _followRepository = followRepository;
        _blockRepository = blockRepository;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
    }

    public async Task<Result> Handle(FollowUserCommand command, CancellationToken cancellationToken)
    {
        Result<Guid> caller = await _currentUser.GetIdAsync(cancellationToken);
        if (caller.IsFailure)
        {
            return caller;
        }

        User? target = await _userRepository.GetByIdForReadAsync(command.UserId, cancellationToken);
        if (target is null)
        {
            return Result.Failure(UserErrors.NotFound);
        }

        Result<UserFollow> follow = UserFollow.Create(caller.Value, target.Id, _timeProvider.GetUtcNow());
        if (follow.IsFailure)
        {
            return follow;
        }

        if (await _blockRepository.ExistsBetweenForReadAsync(caller.Value, target.Id, cancellationToken))
        {
            return Result.Failure(UserBlockErrors.Blocked);
        }

        await _unitOfWork.InTransactionAsync(async () =>
        {
            if (await _followRepository.AddIfMissingAsync(follow.Value, cancellationToken))
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            return true;
        }, cancellationToken);

        return Result.Success();
    }
}
