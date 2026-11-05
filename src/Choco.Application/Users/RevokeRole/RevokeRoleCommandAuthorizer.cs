using Choco.Application.Abstractions.Authorization;
using Choco.Contract.Users;
using Choco.Domain.Roles;

namespace Choco.Application.Users.RevokeRole;

internal sealed class RevokeRoleCommandAuthorizer : IAuthorizer<RevokeRoleCommand>
{
    public PermissionRequirement GetRequirement(RevokeRoleCommand request)
    {
        return new PermissionRequirement(Permission.AssignRoles, PermissionScope.Platform);
    }
}
