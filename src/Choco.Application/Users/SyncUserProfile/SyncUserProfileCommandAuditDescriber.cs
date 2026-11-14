using Choco.Application.Abstractions.Auditing;
using Choco.Contract.Users;
using Choco.Domain.AuditLogs;
using Choco.Domain.Users;
using Choco.SharedKernel.Results;

namespace Choco.Application.Users.SyncUserProfile;

internal sealed class SyncUserProfileCommandAuditDescriber(IUserRepository userRepository, AuditChanges auditChanges)
    : IAuditDescriber<SyncUserProfileCommand, Result>
{
    public async Task<AuditDescription> DescribeAsync(
        SyncUserProfileCommand request, Result response, CancellationToken cancellationToken)
    {
        User user = await userRepository.GetByIdentityIdIncludingDeletedAsync(request.IdentityId, cancellationToken)
            ?? throw new InvalidOperationException("The user synced by the command is missing.");

        return new AuditDescription(
            AuditAction.ProfileUpdated, AuditTargetType.User, user.Id, user.Id, Changes: auditChanges.Get(response));
    }

    public async Task<IReadOnlyList<AuditDescription>> DescribeAllAsync(
        SyncUserProfileCommand request, Result response, CancellationToken cancellationToken)
    {
        return auditChanges.Get(response) is not { Count: > 0 }
            ? []
            : [await DescribeAsync(request, response, cancellationToken)];
    }
}
