using Choco.Application.Abstractions.Authentication;
using Choco.Application.Abstractions.Messaging;
using Choco.Contract.Roles;
using Choco.Domain.Roles;
using Choco.Domain.Users;
using Choco.SharedKernel.Results;

namespace Choco.Application.Roles.GetRoleImpact;

internal sealed class GetRoleImpactQueryHandler(
    ICurrentUser currentUser,
    IRoleRepository roleRepository,
    IUserRepository userRepository) : IQueryHandler<GetRoleImpactQuery, RoleImpactResponse>
{
    public async Task<Result<RoleImpactResponse>> Handle(GetRoleImpactQuery query, CancellationToken cancellationToken)
    {
        Result<Guid> callerId = await currentUser.GetIdAsync(cancellationToken);

        if (callerId.IsFailure)
        {
            return Result<RoleImpactResponse>.Failure(callerId.Error);
        }

        Role? role = await roleRepository.GetByIdForReadAsync(query.Id, cancellationToken);

        if (role is null)
        {
            return Result<RoleImpactResponse>.Failure(RoleErrors.NotFound);
        }

        User? caller = await userRepository.GetByIdWithRolesForReadAsync(callerId.Value, cancellationToken);

        if (caller is null)
        {
            return Result<RoleImpactResponse>.Failure(UserErrors.Deleted);
        }

        IReadOnlyCollection<string> callerPermissions =
            await roleRepository.GetPlatformPermissionsForReadAsync(callerId.Value, cancellationToken);
        Result check = role.CheckCanBeChangedBy(callerPermissions, caller.HoldsRole(role.Id), query.Permissions);

        if (check.IsFailure)
        {
            return Result<RoleImpactResponse>.Failure(check.Error);
        }

        string[] added = query.Permissions.Except(role.Permissions, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        string[] removed = role.Permissions.Except(query.Permissions, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();

        if (added.Length == 0 && removed.Length == 0)
        {
            return Result<RoleImpactResponse>.Success(new RoleImpactResponse(0, [], []));
        }

        int holderCount = await roleRepository.CountHoldersForReadAsync(role, cancellationToken);
        IReadOnlyDictionary<string, int> retained = removed.Length == 0
            ? new Dictionary<string, int>()
            : await roleRepository.CountHoldersKeepingPermissionsForReadAsync(role, removed, cancellationToken);
        RemovedRolePermissionResponse[] removedPermissions = removed
            .Select(code => new RemovedRolePermissionResponse(code, retained.GetValueOrDefault(code)))
            .ToArray();

        return Result<RoleImpactResponse>.Success(new RoleImpactResponse(holderCount, added, removedPermissions));
    }
}
