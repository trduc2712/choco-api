namespace Choco.Domain.Reports;

public sealed record ReportQueueFilter(ReportTargetType? TargetType, ReportReason? Reason);
