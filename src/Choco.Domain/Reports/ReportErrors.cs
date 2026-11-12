using Choco.SharedKernel.Results;

namespace Choco.Domain.Reports;

public static class ReportErrors
{
    public static readonly Error NotFound = Error.NotFound("Report.NotFound", "The report was not found.");
    public static readonly Error OutcomeNotApplicable = Error.Validation(
        "Report.OutcomeNotApplicable", "ContentRemoved applies only to a report about a post.");

    public static readonly Error SelfReport = Error.Validation("Report.SelfReport", "Users cannot report themselves or their own posts.");
}
