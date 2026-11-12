using Choco.Application.Abstractions.Messaging;
using Choco.Contract.Roles;
using Choco.Domain.Roles;
using Choco.SharedKernel.Results;

namespace Choco.Application.Roles.GetRoles;

internal sealed class GetRolesQueryHandler(IRoleRepository roleRepository)
    : IQueryHandler<GetRolesQuery, IReadOnlyList<RoleResponse>>
{
    public async Task<Result<IReadOnlyList<RoleResponse>>> Handle(
        GetRolesQuery query, CancellationToken cancellationToken)
    {
        IReadOnlyList<Role> roles = await roleRepository.ListForReadAsync(cancellationToken);

        IReadOnlyList<RoleResponse> response = roles
            .Select(role => new RoleResponse(
                role.Id,
                role.SystemKey,
                role.Name,
                role.IsDefault,
                role.Permissions.Order(StringComparer.Ordinal).ToList()))
            .ToList();

        return Result<IReadOnlyList<RoleResponse>>.Success(response);
    }
}
