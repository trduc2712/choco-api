using Choco.Application.Abstractions.Auditing;
using Choco.Contract.Users;
using Choco.Domain.AuditLogs;
using Choco.SharedKernel.Results;

namespace Choco.Application.Users.FollowUser;

internal sealed class FollowUserCommandAuditDescriber : IAuditDescriber<FollowUserCommand, Result>
{
    public Task<AuditDescription> DescribeAsync(
        FollowUserCommand request, Result response, CancellationToken cancellationToken)
    {
        return Task.FromResult(new AuditDescription(
            AuditAction.UserFollowed, AuditTargetType.User, request.UserId, request.UserId));
    }
}
