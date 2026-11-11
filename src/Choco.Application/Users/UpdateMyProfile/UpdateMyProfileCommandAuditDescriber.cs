using Choco.Application.Abstractions.Auditing;
using Choco.Contract.Users;
using Choco.Domain.AuditLogs;
using Choco.SharedKernel.Results;

namespace Choco.Application.Users.UpdateMyProfile;

internal sealed class UpdateMyProfileCommandAuditDescriber : IAuditDescriber<UpdateMyProfileCommand, Result<UserProfileResponse>>
{
    public Task<AuditDescription> DescribeAsync(
        UpdateMyProfileCommand request, Result<UserProfileResponse> response, CancellationToken cancellationToken)
    {
        return Task.FromResult(new AuditDescription(
            AuditAction.ProfileUpdated, AuditTargetType.User, response.Value.Id, response.Value.Id));
    }
}
