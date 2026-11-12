using Choco.Contract.Reports;
using Choco.Domain.Reports;

using FluentValidation;

namespace Choco.Application.Reports.ResolveReport;

internal sealed class ResolveReportCommandValidator : AbstractValidator<ResolveReportCommand>
{
    public ResolveReportCommandValidator()
    {
        RuleFor(command => command.ReportId).NotEmpty();

        RuleFor(command => command.Outcome)
            .Must(outcome => ReportEnums.TryParse<ReportOutcome>(outcome, out _))
            .WithMessage("'{PropertyName}' must be one of: Dismissed, ContentRemoved, UserSuspended.");
    }
}
