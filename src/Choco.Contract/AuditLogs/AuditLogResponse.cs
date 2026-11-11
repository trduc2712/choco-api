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
/// <param name="TargetType">The kind of object the action was done to: Post, Comment, User, Report or Feedback.</param>
/// <param name="TargetId">The id of the object the action was done to.</param>
/// <param name="AffectedUser">The user whose data or account the action changed, when it is not the actor; null otherwise.</param>
/// <param name="Detail">A short note that qualifies the action, such as a role name or a new status; null when there is none.</param>
/// <param name="TraceId">The trace id of the request that caused the action, or null when it is unknown.</param>
/// <example>{"id":"00000000-0000-0000-0000-000000000001","occurredAt":"2026-01-01T00:00:00+00:00","actorType":"User","actor":{"id":"00000000-0000-0000-0000-000000000002","username":"username"},"actorRoles":["Administrator","Member"],"clientId":"https://example.com","action":"PostDeleted","targetType":"Post","targetId":"00000000-0000-0000-0000-000000000003","affectedUser":{"id":"00000000-0000-0000-0000-000000000004","username":"username-2"},"detail":null,"traceId":"4bf92f3577b34da6a3ce929d0e0e4736"}</example>
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
    string? TraceId);
