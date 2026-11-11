namespace Choco.Contract.AdminCounts;

/// <summary>
/// The counts the admin sidebar shows.
/// </summary>
/// <param name="NewFeedback">How many pieces of feedback are in the new state. Null when the caller may not read feedback.</param>
/// <param name="OpenReports">How many reports are open. Null when the caller may not handle reports.</param>
/// <example>{ "newFeedback": 12, "openReports": 5 }</example>
public sealed record AdminCountsResponse(int? NewFeedback, int? OpenReports);
