using Choco.Application.Abstractions.Auditing;
using Choco.Contract.Users;
using Choco.Domain.AuditLogs;
using Choco.SharedKernel.Results;

namespace Choco.Application.Users.UnblockUser;

internal sealed class UnblockUserCommandAuditDescriber : IAuditDescriber<UnblockUserCommand, Result>
{
    public Task<AuditDescription> DescribeAsync(
        UnblockUserCommand request, Result response, CancellationToken cancellationToken)
    {
        return Task.FromResult(new AuditDescription(
            AuditAction.UserUnblocked, AuditTargetType.User, request.UserId, request.UserId));
    }
}
