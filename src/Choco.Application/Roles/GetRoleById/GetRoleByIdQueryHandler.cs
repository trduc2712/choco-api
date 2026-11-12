using Choco.Application.Abstractions.Messaging;
using Choco.Contract.Roles;
using Choco.Domain.Roles;
using Choco.SharedKernel.Results;

namespace Choco.Application.Roles.GetRoleById;

internal sealed class GetRoleByIdQueryHandler(IRoleRepository roleRepository)
    : IQueryHandler<GetRoleByIdQuery, RoleResponse>
{
    public async Task<Result<RoleResponse>> Handle(GetRoleByIdQuery query, CancellationToken cancellationToken)
    {
        Role? role = await roleRepository.GetByIdForReadAsync(query.Id, cancellationToken);

        if (role is null)
        {
            return Result<RoleResponse>.Failure(RoleErrors.NotFound);
        }

        return Result<RoleResponse>.Success(role.ToResponse());
    }
}
