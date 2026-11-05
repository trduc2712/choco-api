using Choco.Application.Abstractions.Authorization;
using Choco.Contract.Users;
using Choco.Domain.Roles;

namespace Choco.Application.Users.AssignRole;

internal sealed class AssignRoleCommandAuthorizer : IAuthorizer<AssignRoleCommand>
{
    public PermissionRequirement GetRequirement(AssignRoleCommand request)
    {
        return new PermissionRequirement(Permission.AssignRoles, PermissionScope.Platform);
    }
}
