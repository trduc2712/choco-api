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

        IReadOnlyList<RoleResponse> response = roles.Select(role => role.ToResponse()).ToList();

        return Result<IReadOnlyList<RoleResponse>>.Success(response);
    }
}
