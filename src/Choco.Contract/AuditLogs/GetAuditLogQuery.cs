using Choco.Contract.Abstractions.Messaging;

namespace Choco.Contract.AuditLogs;

/// <summary>Gets one audit log entry with a summary and the recent history of its target.</summary>
/// <param name="Id">The entry's id.</param>
/// <example>{"id":"00000000-0000-0000-0000-000000000001"}</example>
public sealed record GetAuditLogQuery(Guid Id) : IQuery<AuditLogResponse>
{
    public const int ExcerptMaxLength = 160;
    public const int RecentHistorySize = 5;
}
