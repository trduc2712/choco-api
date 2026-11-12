using Choco.Contract.Abstractions.Messaging;

namespace Choco.Contract.Reports;

/// <summary>Resolves a report and carries out its outcome.</summary>
/// <param name="ReportId">The report's id; the route sets it.</param>
/// <param name="Outcome">What was done: Dismissed (nothing else changes), ContentRemoved (deletes the reported post; only for a report about a post) or UserSuspended (suspends the reported user, or the author of the reported post).</param>
/// <example>{"outcome":"ContentRemoved"}</example>
public sealed record ResolveReportCommand(Guid ReportId, string Outcome) : ICommand;
