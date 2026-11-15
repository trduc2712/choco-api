using System.Text.Json.Serialization;

namespace Choco.Contract.AuditLogs;

/// <summary>One entry in the history of a target.</summary>
/// <param name="Id">The entry's id.</param>
/// <param name="Action">What was done, such as FeedbackStatusChanged.</param>
/// <param name="OccurredAt">When the action happened, in UTC.</param>
/// <param name="Value">The value a field took in that entry, such as the new status; left out when the entry records none.</param>
/// <example>{"id":"00000000-0000-0000-0000-000000000001","action":"FeedbackStatusChanged","occurredAt":"2026-01-01T00:00:00+00:00","value":"resolved"}</example>
public sealed record AuditTargetEventResponse(
    Guid Id,
    string Action,
    DateTimeOffset OccurredAt,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Value);
