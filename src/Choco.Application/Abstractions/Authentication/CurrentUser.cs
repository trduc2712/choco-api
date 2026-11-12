using Choco.Contract.Users;
using Choco.Domain.Users;
using Choco.SharedKernel.Results;

using MediatR;

namespace Choco.Application.Abstractions.Authentication;

internal sealed class CurrentUser : ICurrentUser
{
    private readonly IUserContext _userContext;
    private readonly IUserRepository _userRepository;
    private readonly IIdentityProvider _identityProvider;
    private readonly ISender _sender;
    private Result<Snapshot>? _cachedSnapshotResult;

    public CurrentUser(
        IUserContext userContext,
        IUserRepository userRepository,
        IIdentityProvider identityProvider,
        ISender sender)
    {
        _userContext = userContext;
        _userRepository = userRepository;
        _identityProvider = identityProvider;
        _sender = sender;
    }

    public async Task<Result<Guid>> GetIdAsync(CancellationToken cancellationToken = default)
    {
        Result<Snapshot> snapshot = await GetSnapshotAsync(cancellationToken);

        return snapshot.IsFailure ? Result<Guid>.Failure(snapshot.Error) : Result<Guid>.Success(snapshot.Value.Id);
    }

    public async Task<Result<CurrentUserSuspension?>> GetSuspensionAsync(CancellationToken cancellationToken = default)
    {
        Result<Snapshot> snapshot = await GetSnapshotAsync(cancellationToken);

        return snapshot.IsFailure
            ? Result<CurrentUserSuspension?>.Failure(snapshot.Error)
            : Result<CurrentUserSuspension?>.Success(snapshot.Value.Suspension);
    }

    private async Task<Result<Snapshot>> GetSnapshotAsync(CancellationToken cancellationToken)
    {
        return _cachedSnapshotResult ??= await FindOrCreateUserAsync(cancellationToken);
    }

    private async Task<Result<Snapshot>> FindOrCreateUserAsync(CancellationToken cancellationToken)
    {
        string identityId = _userContext.IdentityId;

        User? user = await _userRepository.GetByIdentityIdIncludingDeletedAsync(identityId, cancellationToken);

        if (user is null)
        {
            IdpUser? idpUser = await _identityProvider.GetUserAsync(identityId, cancellationToken);

            if (idpUser is null)
            {
                return Result<Snapshot>.Failure(UserErrors.Deleted);
            }

            Result createUserResult = await _sender.Send(
                new CreateUserCommand(idpUser.IdentityId, idpUser.Username ?? string.Empty, idpUser.FullName, idpUser.AvatarUrl),
                cancellationToken);

            if (createUserResult.IsFailure)
            {
                return Result<Snapshot>.Failure(createUserResult.Error);
            }

            user = await _userRepository.GetByIdentityIdIncludingDeletedAsync(identityId, cancellationToken);
        }

        if (user is null || user.IsDeleted)
        {
            return Result<Snapshot>.Failure(UserErrors.Deleted);
        }

        CurrentUserSuspension? suspension = user.SuspendedAt is { } suspendedAt && user.SuspensionReason is { } reason
            ? new CurrentUserSuspension(reason, suspendedAt)
            : null;

        return Result<Snapshot>.Success(new Snapshot(user.Id, suspension));
    }

    private sealed record Snapshot(Guid Id, CurrentUserSuspension? Suspension);
}
