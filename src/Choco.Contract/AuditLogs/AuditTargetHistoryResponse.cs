namespace Choco.Contract.AuditLogs;

/// <summary>The audit log entries about the same target as one entry.</summary>
/// <param name="TotalCount">The number of entries about the target, this one included.</param>
/// <param name="Recent">Up to 5 entries about the target around this one, this one included, oldest first.</param>
/// <example>{"totalCount":7,"recent":[{"id":"00000000-0000-0000-0000-000000000001","action":"FeedbackSent","occurredAt":"2026-01-01T00:00:00+00:00"},{"id":"00000000-0000-0000-0000-000000000002","action":"FeedbackStatusChanged","occurredAt":"2026-01-02T00:00:00+00:00","value":"resolved"}]}</example>
public sealed record AuditTargetHistoryResponse(int TotalCount, IReadOnlyList<AuditTargetEventResponse> Recent);
