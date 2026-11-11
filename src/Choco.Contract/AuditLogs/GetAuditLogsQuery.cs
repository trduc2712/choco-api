using Choco.Contract.Abstractions;
using Choco.Contract.Abstractions.Messaging;

namespace Choco.Contract.AuditLogs;

/// <summary>Lists audit log entries, newest first.</summary>
/// <param name="ActorId">Only entries made by this user. Null for any actor.</param>
/// <param name="Action">Only entries of this action, such as PostDeleted or RoleAssigned. Null for any action.</param>
/// <param name="TargetType">Only entries about this kind of object: Post, Comment, User, Report or Feedback. Null for any kind.</param>
/// <param name="TargetId">Only entries about the object with this id. Null for any object.</param>
/// <param name="AffectedUserId">Only entries that affected this user. Null for any user.</param>
/// <param name="From">Only entries at or after this time. Null for no lower limit.</param>
/// <param name="To">Only entries before this time. Null for no upper limit.</param>
/// <param name="Cursor">The previous page's nextCursor, or null for the first page.</param>
/// <param name="Limit">The page size, from 1 to 50. Defaults to 20 at the endpoint.</param>
/// <example>{"actorId":null,"action":"PostDeleted","targetType":"Post","targetId":null,"affectedUserId":null,"from":"2026-01-01T00:00:00+00:00","to":null,"cursor":null,"limit":20}</example>
public sealed record GetAuditLogsQuery(
    Guid? ActorId,
    string? Action,
    string? TargetType,
    Guid? TargetId,
    Guid? AffectedUserId,
    DateTimeOffset? From,
    DateTimeOffset? To,
    string? Cursor,
    int Limit) : IQuery<PageResponse<AuditLogResponse>>
{
    public const int DefaultLimit = 20;
    public const int MaxLimit = 50;
}
