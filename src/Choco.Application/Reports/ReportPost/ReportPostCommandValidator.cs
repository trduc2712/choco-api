using Choco.Contract.Reports;
using Choco.Domain.Reports;

using FluentValidation;

namespace Choco.Application.Reports.ReportPost;

internal sealed class ReportPostCommandValidator : AbstractValidator<ReportPostCommand>
{
    public ReportPostCommandValidator()
    {
        RuleFor(command => command.PostId).NotEmpty();

        RuleFor(command => command.Reason)
            .Must(reason => ReportEnums.TryParse<ReportReason>(reason, out _))
            .WithMessage("'{PropertyName}' must be one of: Spam, Harassment, HateSpeech, Violence, Other.");

        RuleFor(command => command.Details).MaximumLength(Report.DetailsMaxLength);
    }
}
