using Choco.Application.Abstractions.Auditing;
using Choco.Contract.Users;
using Choco.Domain.AuditLogs;
using Choco.Domain.Users;
using Choco.SharedKernel.Results;

namespace Choco.Application.Users.SyncUserDeletion;

internal sealed class SyncUserDeletionCommandAuditDescriber(IUserRepository userRepository, AuditChanges auditChanges)
    : IAuditDescriber<SyncUserDeletionCommand, Result>
{
    public async Task<AuditDescription> DescribeAsync(
        SyncUserDeletionCommand request, Result response, CancellationToken cancellationToken)
    {
        User user = await userRepository.GetByIdentityIdIncludingDeletedAsync(request.IdentityId, cancellationToken)
            ?? throw new InvalidOperationException("The user deleted by the command is missing.");

        return new AuditDescription(AuditAction.AccountDeleted, AuditTargetType.User, user.Id, user.Id);
    }

    public async Task<IReadOnlyList<AuditDescription>> DescribeAllAsync(
        SyncUserDeletionCommand request, Result response, CancellationToken cancellationToken)
    {
        return auditChanges.Get(response) is null ? [] : [await DescribeAsync(request, response, cancellationToken)];
    }
}
