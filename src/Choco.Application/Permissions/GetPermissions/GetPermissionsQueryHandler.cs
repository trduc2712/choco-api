using Choco.Application.Abstractions.Messaging;
using Choco.Contract.Permissions;
using Choco.Domain.Roles;
using Choco.SharedKernel.Results;

namespace Choco.Application.Permissions.GetPermissions;

internal sealed class GetPermissionsQueryHandler
    : IQueryHandler<GetPermissionsQuery, IReadOnlyList<PermissionResponse>>
{
    public Task<Result<IReadOnlyList<PermissionResponse>>> Handle(
        GetPermissionsQuery query, CancellationToken cancellationToken)
    {
        IReadOnlyList<PermissionResponse> response = Permission.All
            .Order(StringComparer.Ordinal)
            .Select(code => new PermissionResponse(code))
            .ToList();

        return Task.FromResult(Result<IReadOnlyList<PermissionResponse>>.Success(response));
    }
}
