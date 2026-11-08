using Choco.Application.Abstractions.Authentication;
using Choco.Application.Abstractions.Messaging;
using Choco.Contract.Users;
using Choco.Domain.UserFollows;
using Choco.Domain.Users;
using Choco.SharedKernel.Results;

namespace Choco.Application.Users.UnfollowUser;

internal sealed class UnfollowUserCommandHandler : ICommandHandler<UnfollowUserCommand>
{
    private readonly ICurrentUser _currentUser;
    private readonly IUserRepository _userRepository;
    private readonly IUserFollowRepository _followRepository;

    public UnfollowUserCommandHandler(
        ICurrentUser currentUser,
        IUserRepository userRepository,
        IUserFollowRepository followRepository)
    {
        _currentUser = currentUser;
        _userRepository = userRepository;
        _followRepository = followRepository;
    }

    public async Task<Result> Handle(UnfollowUserCommand command, CancellationToken cancellationToken)
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

        await _followRepository.RemoveIfPresentAsync(caller.Value, target.Id, cancellationToken);
        return Result.Success();
    }
}
