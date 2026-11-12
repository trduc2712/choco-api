using Choco.Contract.Abstractions;
using Choco.Contract.Abstractions.Messaging;

namespace Choco.Contract.AuditLogs;

/// <summary>Lists audit log entries, newest first.</summary>
/// <param name="ActorId">Only entries made by this user. Null for any actor.</param>
/// <param name="Action">Only entries of this action, such as PostDeleted or RoleAssigned. Null for any action.</param>
/// <param name="TargetType">Only entries about this kind of object: Post, Comment, User, Report, Feedback or Role. Null for any kind.</param>
/// <param name="TargetId">Only entries about the object with this id. Null for any object.</param>
/// <param name="AffectedUserId">Only entries that affected this user. Null for any user.</param>
/// <param name="From">Only entries at or after this time. Null for no lower limit.</param>
/// <param name="To">Only entries before this time. Null for no upper limit.</param>
/// <param name="Page">The page number, from 1. Defaults to 1 at the endpoint.</param>
/// <param name="PageSize">The page size, from 1 to 50. Defaults to 20 at the endpoint.</param>
/// <example>{"actorId":null,"action":"PostDeleted","targetType":"Post","targetId":null,"affectedUserId":null,"from":"2026-01-01T00:00:00+00:00","to":null,"page":1,"pageSize":20}</example>
public sealed record GetAuditLogsQuery(
    Guid? ActorId,
    string? Action,
    string? TargetType,
    Guid? TargetId,
    Guid? AffectedUserId,
    DateTimeOffset? From,
    DateTimeOffset? To,
    int Page,
    int PageSize) : IQuery<OffsetPageResponse<AuditLogResponse>>
{
    public const int DefaultPage = 1;
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 50;
}
