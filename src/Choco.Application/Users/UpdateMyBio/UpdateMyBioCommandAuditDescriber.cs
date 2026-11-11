using Choco.Application.Abstractions.Auditing;
using Choco.Contract.Users;
using Choco.Domain.AuditLogs;
using Choco.SharedKernel.Results;

namespace Choco.Application.Users.UpdateMyBio;

internal sealed class UpdateMyBioCommandAuditDescriber : IAuditDescriber<UpdateMyBioCommand, Result<UserProfileResponse>>
{
    public Task<AuditDescription> DescribeAsync(
        UpdateMyBioCommand request, Result<UserProfileResponse> response, CancellationToken cancellationToken)
    {
        return Task.FromResult(new AuditDescription(
            AuditAction.BioUpdated, AuditTargetType.User, response.Value.Id, response.Value.Id));
    }
}
