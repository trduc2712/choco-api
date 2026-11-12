namespace Choco.Domain.Reports;

public sealed record ReportQueueGroup(ReportTargetType TargetType, Guid TargetId, IReadOnlyList<Report> OpenReports);
