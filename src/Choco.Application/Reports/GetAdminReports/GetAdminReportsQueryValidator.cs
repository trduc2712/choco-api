using Choco.Contract.Reports;
using Choco.Domain.Reports;

using FluentValidation;

namespace Choco.Application.Reports.GetAdminReports;

internal sealed class GetAdminReportsQueryValidator : AbstractValidator<GetAdminReportsQuery>
{
    public GetAdminReportsQueryValidator()
    {
        RuleFor(query => query.Page).GreaterThanOrEqualTo(1);

        RuleFor(query => query.PageSize).InclusiveBetween(1, GetAdminReportsQuery.MaxPageSize);

        RuleFor(query => query.TargetType)
            .Must(targetType => ReportEnums.TryParse<ReportTargetType>(targetType, out _))
            .When(query => query.TargetType is not null)
            .WithMessage("'{PropertyName}' must be one of: Post, User.");

        RuleFor(query => query.TargetId)
            .Must(targetId => targetId != Guid.Empty)
            .When(query => query.TargetId is not null)
            .WithMessage("'{PropertyName}' must not be empty.");

        RuleFor(query => query.Status)
            .Must(status => ReportEnums.TryParse<ReportStatus>(status, out _))
            .When(query => query.Status is not null)
            .WithMessage("'{PropertyName}' must be one of: Open, Resolved.");

        RuleFor(query => query.Reason)
            .Must(reason => ReportEnums.TryParse<ReportReason>(reason, out _))
            .When(query => query.Reason is not null)
            .WithMessage("'{PropertyName}' must be one of: Spam, Harassment, HateSpeech, Violence, Other.");

        RuleFor(query => query.Outcome)
            .Must(outcome => ReportEnums.TryParse<ReportOutcome>(outcome, out _))
            .When(query => query.Outcome is not null)
            .WithMessage("'{PropertyName}' must be one of: Dismissed, ContentRemoved, UserSuspended.");
    }
}
