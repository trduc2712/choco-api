using Choco.Application.Abstractions.Requests;
using Choco.Domain.AuditLogs;
using Choco.Domain.Roles;

namespace Choco.Application.Abstractions.Auditing;

internal sealed class AuditEntryBuilder(
    IRequestContext requestContext,
    IRoleRepository roleRepository,
    TimeProvider timeProvider)
{
    public async Task<AuditLogEntry> CreateAsync(
        AuditActorType actorType,
        Guid? actorId,
        IReadOnlyCollection<string> actorRoles,
        AuditDescription description,
        CancellationToken cancellationToken)
    {
        Guid? affectedUserId = description.AffectedUserId == actorId
            ? null
            : description.AffectedUserId;

        IReadOnlyCollection<string> affectedUserRoles = affectedUserId is { } userId
            ? await roleRepository.GetPlatformRoleNamesForReadAsync(userId, cancellationToken)
            : [];

        return AuditLogEntry.Create(
            timeProvider.GetUtcNow(),
            actorType,
            actorId,
            actorRoles,
            requestContext.ClientId,
            description.Action,
            description.TargetType,
            description.TargetId,
            affectedUserId,
            description.Detail,
            requestContext.TraceId,
            requestContext.UserAgent,
            requestContext.IpAddress,
            description.Changes,
            affectedUserRoles);
    }
}
