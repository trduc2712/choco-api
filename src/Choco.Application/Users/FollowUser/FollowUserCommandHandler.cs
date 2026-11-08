using Choco.Application.Abstractions.Authentication;
using Choco.Application.Abstractions.Messaging;
using Choco.Contract.Users;
using Choco.Domain.UserFollows;
using Choco.Domain.Users;
using Choco.SharedKernel.Results;

namespace Choco.Application.Users.FollowUser;

internal sealed class FollowUserCommandHandler : ICommandHandler<FollowUserCommand>
{
    private readonly ICurrentUser _currentUser;
    private readonly IUserRepository _userRepository;
    private readonly IUserFollowRepository _followRepository;
    private readonly TimeProvider _timeProvider;

    public FollowUserCommandHandler(
        ICurrentUser currentUser,
        IUserRepository userRepository,
        IUserFollowRepository followRepository,
        TimeProvider timeProvider)
    {
        _currentUser = currentUser;
        _userRepository = userRepository;
        _followRepository = followRepository;
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

        await _followRepository.AddIfMissingAsync(follow.Value, cancellationToken);
        return Result.Success();
    }
}
