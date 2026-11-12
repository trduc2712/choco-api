using Choco.Application.Abstractions.Auditing;
using Choco.Contract.Users;
using Choco.Domain.AuditLogs;
using Choco.SharedKernel.Results;

namespace Choco.Application.Users.SuspendUser;

internal sealed class SuspendUserCommandAuditDescriber : IAuditDescriber<SuspendUserCommand, Result>
{
    public Task<AuditDescription> DescribeAsync(
        SuspendUserCommand request, Result response, CancellationToken cancellationToken)
    {
        return Task.FromResult(new AuditDescription(
            AuditAction.UserSuspended,
            AuditTargetType.User,
            request.UserId,
            request.UserId,
            $"Reason: {request.Reason.Trim()}"));
    }
}
