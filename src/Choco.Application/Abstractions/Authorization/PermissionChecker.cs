using Choco.Application.Abstractions.Authentication;
using Choco.Domain.Roles;
using Choco.SharedKernel.Results;

namespace Choco.Application.Abstractions.Authorization;

internal sealed class PermissionChecker : IPermissionChecker
{
    private readonly ICurrentUser _currentUser;
    private readonly IRoleRepository _roleRepository;

    public PermissionChecker(ICurrentUser currentUser, IRoleRepository roleRepository)
    {
        _currentUser = currentUser;
        _roleRepository = roleRepository;
    }

    public async Task<Result<bool>> IsGrantedToCurrentUserAsync(
        string permission, PermissionScope scope, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permission);
        ArgumentNullException.ThrowIfNull(scope);

        Result<Guid> userIdResult = await _currentUser.GetIdAsync(cancellationToken);

        if (userIdResult.IsFailure)
        {
            return Result<bool>.Failure(userIdResult.Error);
        }

        IReadOnlyCollection<string> permissions =
            await _roleRepository.GetPlatformPermissionsForReadAsync(userIdResult.Value, cancellationToken);

        return Result<bool>.Success(permissions.Contains(permission));
    }
}
