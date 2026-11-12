using Choco.Contract.Reports;
using Choco.Domain.Reports;

using FluentValidation;

namespace Choco.Application.Reports.GetReports;

internal sealed class GetReportsQueryValidator : AbstractValidator<GetReportsQuery>
{
    public GetReportsQueryValidator()
    {
        RuleFor(query => query.Page).GreaterThanOrEqualTo(1);

        RuleFor(query => query.PageSize).InclusiveBetween(1, GetReportsQuery.MaxPageSize);

        RuleFor(query => query.TargetType)
            .Must(targetType => ReportEnums.TryParse<ReportTargetType>(targetType, out _))
            .When(query => query.TargetType is not null)
            .WithMessage("'{PropertyName}' must be one of: Post, User.");

        RuleFor(query => query.Reason)
            .Must(reason => ReportEnums.TryParse<ReportReason>(reason, out _))
            .When(query => query.Reason is not null)
            .WithMessage("'{PropertyName}' must be one of: Spam, Harassment, HateSpeech, Violence, Other.");
    }
}
