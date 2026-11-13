using Choco.Application.Abstractions.Auditing;
using Choco.Contract.Users;
using Choco.Domain.AuditLogs;
using Choco.SharedKernel.Results;

namespace Choco.Application.Users.UploadMyAvatar;

internal sealed class UploadMyAvatarCommandAuditDescriber(AuditChanges auditChanges) : IAuditDescriber<UploadMyAvatarCommand, Result<UserProfileResponse>>
{
    public Task<AuditDescription> DescribeAsync(
        UploadMyAvatarCommand request, Result<UserProfileResponse> response, CancellationToken cancellationToken)
    {
        return Task.FromResult(new AuditDescription(
            AuditAction.AvatarUploaded, AuditTargetType.User, response.Value.Id, response.Value.Id,
            Changes: auditChanges.Get(response)));
    }
}
