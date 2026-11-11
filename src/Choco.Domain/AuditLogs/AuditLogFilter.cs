namespace Choco.Domain.AuditLogs;

public sealed record AuditLogFilter(
    Guid? ActorId,
    AuditAction? Action,
    AuditTargetType? TargetType,
    Guid? TargetId,
    Guid? AffectedUserId,
    DateTimeOffset? From,
    DateTimeOffset? To);
