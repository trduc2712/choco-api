using Choco.Application.Abstractions.Authorization;
using Choco.Contract.Roles;
using Choco.Domain.Roles;

namespace Choco.Application.Roles.CreateRole;

internal sealed class CreateRoleCommandAuthorizer : IAuthorizer<CreateRoleCommand>
{
    public PermissionRequirement GetRequirement(CreateRoleCommand request)
    {
        return new PermissionRequirement(Permission.CreateRoles, PermissionScope.Platform);
    }
}
