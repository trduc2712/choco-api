using System.Text.Json.Serialization;

namespace Choco.Contract.AuditLogs;

/// <summary>
/// One audit log entry: who did what to which object, as an administrator sees it.
/// </summary>
/// <param name="Id">The entry's id.</param>
/// <param name="OccurredAt">When the action happened, in UTC.</param>
/// <param name="ActorType">Who performed the action: User, or System for work no user triggered.</param>
/// <param name="Actor">The user who performed the action; null when the actor is the system.</param>
/// <param name="ActorRoles">The names of the roles the actor held when the action happened.</param>
/// <param name="ClientId">The client application that sent the request, or null when it is unknown.</param>
/// <param name="Action">What was done, such as PostDeleted or RoleAssigned.</param>
/// <param name="TargetType">The kind of object the action was done to: Post, Comment, User, Report, Feedback, Role, Request or AuditLog.</param>
/// <param name="TargetId">The id of the object the action was done to.</param>
/// <param name="AffectedUser">The user whose data or account the action changed, when it is not the actor; null otherwise.</param>
/// <param name="Detail">A short note that qualifies the action, such as a role name or a new status; null when there is none.</param>
/// <param name="Changes">Fields changed by the action. Empty when no changes were recorded.</param>
/// <param name="TraceId">The trace id of the request that caused the action, or null when it is unknown.</param>
/// <param name="AffectedUserRoles">The names of the roles the affected user held right after the action. Empty when there is no affected user or the entry is older than this field.</param>
/// <param name="TargetSummary">The current state of the target. Only in the single-entry response; left out when the target no longer exists or its type has no summary (Request, AuditLog).</param>
/// <param name="TargetHistory">The other entries about the same target. Only in the single-entry response.</param>
/// <param name="Reason">The reason supplied when updating or deleting a role, or null.</param>
/// <example>{"id":"00000000-0000-0000-0000-000000000001","occurredAt":"2026-01-01T00:00:00+00:00","actorType":"User","actor":{"id":"00000000-0000-0000-0000-000000000002","username":"username"},"actorRoles":["Administrator","Member"],"clientId":"https://example.com","action":"RoleUpdated","targetType":"Role","targetId":"00000000-0000-0000-0000-000000000003","affectedUser":null,"detail":"Role: Role Name","changes":[],"traceId":"4bf92f3577b34da6a3ce929d0e0e4736","affectedUserRoles":[],"reason":"Role change reason"}</example>
public sealed record AuditLogResponse(
    Guid Id,
    DateTimeOffset OccurredAt,
    string ActorType,
    AuditLogUserResponse? Actor,
    IReadOnlyList<string> ActorRoles,
    string? ClientId,
    string Action,
    string TargetType,
    Guid TargetId,
    AuditLogUserResponse? AffectedUser,
    string? Detail,
    string? TraceId,
    IReadOnlyList<AuditChangeResponse> Changes,
    IReadOnlyList<string> AffectedUserRoles,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] AuditTargetSummaryResponse? TargetSummary = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] AuditTargetHistoryResponse? TargetHistory = null,
    string? Reason = null);
