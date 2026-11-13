using Choco.Application.Abstractions.Auditing;
using Choco.Contract.Users;
using Choco.Domain.AuditLogs;
using Choco.SharedKernel.Results;

namespace Choco.Application.Users.RemoveMyAvatar;

internal sealed class RemoveMyAvatarCommandAuditDescriber(AuditChanges auditChanges) : IAuditDescriber<RemoveMyAvatarCommand, Result<UserProfileResponse>>
{
    public Task<AuditDescription> DescribeAsync(
        RemoveMyAvatarCommand request, Result<UserProfileResponse> response, CancellationToken cancellationToken)
    {
        return Task.FromResult(new AuditDescription(
            AuditAction.AvatarRemoved, AuditTargetType.User, response.Value.Id, response.Value.Id,
            Changes: auditChanges.Get(response)));
    }
}
