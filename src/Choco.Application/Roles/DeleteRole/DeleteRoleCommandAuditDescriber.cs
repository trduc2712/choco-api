using Choco.Application.Abstractions.Auditing;
using Choco.Contract.Roles;
using Choco.Domain.AuditLogs;
using Choco.Domain.Roles;
using Choco.Domain.Users;
using Choco.SharedKernel.Results;

namespace Choco.Application.Roles.DeleteRole;

internal sealed class DeleteRoleCommandAuditDescriber(IRoleRepository roleRepository, IUserRepository userRepository)
    : IAuditDescriber<DeleteRoleCommand, Result<RoleResponse>>
{
    private IReadOnlyList<(Guid UserId, bool HeldBefore)> _holders = [];

    public async Task CaptureAsync(DeleteRoleCommand request, CancellationToken cancellationToken)
    {
        if (request.ReassignTo is { } targetId && targetId != request.Id)
        {
            IReadOnlyList<User> holders = await userRepository.ListWithRoleAsync(request.Id, cancellationToken);
            _holders = holders.Select(holder => (holder.Id, holder.HoldsRole(targetId))).ToArray();
        }
    }

    public async Task<IReadOnlyList<AuditDescription>> DescribeAllAsync(
        DeleteRoleCommand request, Result<RoleResponse> response, CancellationToken cancellationToken)
    {
        List<AuditDescription> descriptions = [await DescribeAsync(request, response, cancellationToken)];

        if (request.ReassignTo is { } targetId && _holders.Count > 0)
        {
            Role? target = await roleRepository.GetByIdForReadAsync(targetId, cancellationToken);
            string name = target?.Name ?? targetId.ToString();
            descriptions.AddRange(_holders.Select(holder => new AuditDescription(
                AuditAction.RoleAssigned,
                AuditTargetType.User,
                holder.UserId,
                holder.UserId,
                $"Role: {name}",
                holder.HeldBefore ? [] : [new AuditChange("roles", Added: [name])])));
        }

        return descriptions;
    }

    public Task<AuditDescription> DescribeAsync(
        DeleteRoleCommand request, Result<RoleResponse> response, CancellationToken cancellationToken)
    {
        return Task.FromResult(new AuditDescription(
            AuditAction.RoleDeleted,
            AuditTargetType.Role,
            response.Value.Id,
            Detail: $"Role: {response.Value.Name}",
            Changes: RoleAuditChanges.Between(response.Value, null),
            Reason: request.Reason));
    }
}
