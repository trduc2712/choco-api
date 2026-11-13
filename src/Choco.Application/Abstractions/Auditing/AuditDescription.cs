using Choco.Domain.AuditLogs;

namespace Choco.Application.Abstractions.Auditing;

public sealed record AuditDescription(
    AuditAction Action,
    AuditTargetType TargetType,
    Guid TargetId,
    Guid? AffectedUserId = null,
    string? Detail = null,
    IReadOnlyList<AuditChange>? Changes = null);
