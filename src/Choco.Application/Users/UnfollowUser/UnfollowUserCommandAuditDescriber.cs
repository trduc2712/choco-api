using Choco.Application.Abstractions.Auditing;
using Choco.Contract.Users;
using Choco.Domain.AuditLogs;
using Choco.SharedKernel.Results;

namespace Choco.Application.Users.UnfollowUser;

internal sealed class UnfollowUserCommandAuditDescriber : IAuditDescriber<UnfollowUserCommand, Result>
{
    public Task<AuditDescription> DescribeAsync(
        UnfollowUserCommand request, Result response, CancellationToken cancellationToken)
    {
        return Task.FromResult(new AuditDescription(
            AuditAction.UserUnfollowed, AuditTargetType.User, request.UserId, request.UserId));
    }
}
