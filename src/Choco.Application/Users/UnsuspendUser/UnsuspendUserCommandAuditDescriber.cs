using Choco.Application.Abstractions.Auditing;
using Choco.Contract.Users;
using Choco.Domain.AuditLogs;
using Choco.SharedKernel.Results;

namespace Choco.Application.Users.UnsuspendUser;

internal sealed class UnsuspendUserCommandAuditDescriber(AuditChanges auditChanges) : IAuditDescriber<UnsuspendUserCommand, Result>
{
    public Task<AuditDescription> DescribeAsync(
        UnsuspendUserCommand request, Result response, CancellationToken cancellationToken)
    {
        return Task.FromResult(new AuditDescription(
            AuditAction.UserUnsuspended, AuditTargetType.User, request.UserId, request.UserId,
            Changes: auditChanges.Get(response)));
    }
}
