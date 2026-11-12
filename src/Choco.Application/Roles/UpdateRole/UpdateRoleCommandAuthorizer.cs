using Choco.Application.Abstractions.Authorization;
using Choco.Contract.Roles;
using Choco.Domain.Roles;

namespace Choco.Application.Roles.UpdateRole;

internal sealed class UpdateRoleCommandAuthorizer : IAuthorizer<UpdateRoleCommand>
{
    public PermissionRequirement GetRequirement(UpdateRoleCommand request)
    {
        return new PermissionRequirement(Permission.UpdateRoles, PermissionScope.Platform);
    }
}
