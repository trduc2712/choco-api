using Choco.Application.Abstractions.Auditing;
using Choco.Contract.Roles;
using Choco.Domain.AuditLogs;
using Choco.SharedKernel.Results;

namespace Choco.Application.Roles.CreateRole;

internal sealed class CreateRoleCommandAuditDescriber : IAuditDescriber<CreateRoleCommand, Result<RoleResponse>>
{
    public Task<AuditDescription> DescribeAsync(
        CreateRoleCommand request, Result<RoleResponse> response, CancellationToken cancellationToken)
    {
        return Task.FromResult(new AuditDescription(
            AuditAction.RoleCreated,
            AuditTargetType.Role,
            response.Value.Id,
            Detail: $"Role: {response.Value.Name}"));
    }
}
