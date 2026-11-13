using Choco.Application.Abstractions.Requests;
using Choco.Domain.AuditLogs;

namespace Choco.Application.Abstractions.Auditing;

internal sealed class AuditEntryBuilder(IRequestContext requestContext, TimeProvider timeProvider)
{
    public AuditLogEntry Create(
        AuditActorType actorType,
        Guid? actorId,
        IReadOnlyCollection<string> actorRoles,
        AuditDescription description)
    {
        Guid? affectedUserId = description.AffectedUserId == actorId
            ? null
            : description.AffectedUserId;

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
            requestContext.IpAddress);
    }
}
