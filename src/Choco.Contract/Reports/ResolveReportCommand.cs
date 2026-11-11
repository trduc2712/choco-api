using Choco.Contract.Abstractions.Messaging;

namespace Choco.Contract.Reports;

/// <summary>Resolves a report.</summary>
/// <param name="ReportId">The report's id; the route sets it.</param>
/// <param name="Outcome">How the report was handled: Dismissed or ActionTaken.</param>
/// <example>{"outcome":"Dismissed"}</example>
public sealed record ResolveReportCommand(Guid ReportId, string Outcome) : ICommand;
