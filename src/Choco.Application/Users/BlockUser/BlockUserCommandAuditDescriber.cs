using Choco.Application.Abstractions.Auditing;
using Choco.Contract.Users;
using Choco.Domain.AuditLogs;
using Choco.SharedKernel.Results;

namespace Choco.Application.Users.BlockUser;

internal sealed class BlockUserCommandAuditDescriber : IAuditDescriber<BlockUserCommand, Result>
{
    public Task<AuditDescription> DescribeAsync(
        BlockUserCommand request, Result response, CancellationToken cancellationToken)
    {
        return Task.FromResult(new AuditDescription(
            AuditAction.UserBlocked, AuditTargetType.User, request.UserId, request.UserId));
    }
}
