namespace Choco.Domain.Reports;

public sealed record ReportListFilter(
    ReportTargetType? TargetType,
    Guid? TargetId,
    ReportStatus? Status,
    ReportReason? Reason,
    ReportOutcome? Outcome);
