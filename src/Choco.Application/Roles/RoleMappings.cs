using Choco.Contract.Roles;
using Choco.Domain.Roles;

namespace Choco.Application.Roles;

internal static class RoleMappings
{
    public static RoleResponse ToResponse(this Role role)
    {
        return new RoleResponse(
            role.Id,
            role.SystemKey,
            role.Name,
            role.IsDefault,
            role.Permissions.Order(StringComparer.Ordinal).ToList());
    }
}
