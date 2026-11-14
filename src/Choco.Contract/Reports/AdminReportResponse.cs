namespace Choco.Contract.Reports;

/// <summary>One report, as a moderator sees it.</summary>
/// <param name="Id">The report's id.</param>
/// <param name="TargetType">What was reported: Post or User.</param>
/// <param name="TargetId">The id of the reported post or user.</param>
/// <param name="Reason">Why it was reported: Spam, Harassment, HateSpeech, Violence or Other.</param>
/// <param name="Details">The reporter's explanation, or null when none was given.</param>
/// <param name="Reporter">The user who made the report.</param>
/// <param name="CreatedAt">When it was reported, in UTC.</param>
/// <param name="Status">Open or Resolved.</param>
/// <param name="Outcome">What was done when the report was resolved: Dismissed, ContentRemoved or UserSuspended. Null while the report is open.</param>
/// <param name="Resolver">The user who resolved the report. Null while the report is open.</param>
/// <param name="ResolvedAt">When the report was resolved, in UTC. Null while the report is open.</param>
/// <example>{"id":"00000000-0000-0000-0000-000000000001","targetType":"Post","targetId":"00000000-0000-0000-0000-000000000002","reason":"Spam","details":"details","reporter":{"id":"00000000-0000-0000-0000-000000000003","username":"username"},"createdAt":"2026-01-01T00:00:00+00:00","status":"Resolved","outcome":"ContentRemoved","resolver":{"id":"00000000-0000-0000-0000-000000000004","username":"username-2"},"resolvedAt":"2026-01-02T00:00:00+00:00"}</example>
public sealed record AdminReportResponse(
    Guid Id,
    string TargetType,
    Guid TargetId,
    string Reason,
    string? Details,
    ReportUserResponse Reporter,
    DateTimeOffset CreatedAt,
    string Status,
    string? Outcome,
    ReportUserResponse? Resolver,
    DateTimeOffset? ResolvedAt);
