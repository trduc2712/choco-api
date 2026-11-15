using Choco.Contract.AuditLogs;
using Choco.Domain.AuditLogs;

namespace Choco.Application.AuditLogs;

internal static class AuditLogMappings
{
    public static AuditLogResponse ToResponse(
        this AuditLogEntry entry,
        IReadOnlyDictionary<Guid, string> usernames,
        IReadOnlyDictionary<string, DateTimeOffset>? previousChanges = null,
        AuditTargetSummaryResponse? targetSummary = null,
        AuditTargetHistoryResponse? targetHistory = null)
    {
        return new AuditLogResponse(
            entry.Id,
            entry.OccurredAt,
            entry.ActorType.ToString(),
            ToUser(entry.ActorId, usernames),
            entry.ActorRoles,
            entry.ClientId,
            entry.Action.ToString(),
            entry.TargetType.ToString(),
            entry.TargetId,
            ToUser(entry.AffectedUserId, usernames),
            entry.Detail,
            entry.TraceId,
            entry.Changes.Select(change => new AuditChangeResponse(
                change.Field,
                change.Before,
                change.After,
                change.Added,
                change.Removed,
                previousChanges is not null && previousChanges.TryGetValue(change.Field, out DateTimeOffset changedAt)
                    ? changedAt
                    : null)).ToArray(),
            entry.AffectedUserRoles,
            targetSummary,
            targetHistory);
    }

    public static AuditLogUserResponse? ToUser(Guid? userId, IReadOnlyDictionary<Guid, string> usernames)
    {
        return userId is { } id ? new AuditLogUserResponse(id, usernames.GetValueOrDefault(id)) : null;
    }
}
