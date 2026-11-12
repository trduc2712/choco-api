namespace Choco.Contract.Reports;

/// <summary>One open report behind a row of the report queue.</summary>
/// <param name="Id">The report's id.</param>
/// <param name="Reason">Why it was reported: Spam, Harassment, HateSpeech, Violence or Other.</param>
/// <param name="Details">The reporter's explanation, or null when none was given.</param>
/// <param name="ReporterId">The reporting user's id.</param>
/// <param name="ReporterUsername">The reporting user's username, or null when that account is deleted.</param>
/// <param name="CreatedAt">When it was reported, in UTC.</param>
/// <example>{"id":"00000000-0000-0000-0000-000000000001","reason":"Spam","details":"details","reporterId":"00000000-0000-0000-0000-000000000003","reporterUsername":"username","createdAt":"2026-01-01T00:00:00+00:00"}</example>
public sealed record ReportDetailResponse(
    Guid Id,
    string Reason,
    string? Details,
    Guid ReporterId,
    string? ReporterUsername,
    DateTimeOffset CreatedAt);
