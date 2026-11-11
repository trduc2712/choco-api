using Choco.Contract.Reports;
using Choco.Domain.Reports;

using FluentValidation;

namespace Choco.Application.Reports.ReportUser;

internal sealed class ReportUserCommandValidator : AbstractValidator<ReportUserCommand>
{
    public ReportUserCommandValidator()
    {
        RuleFor(command => command.UserId).NotEmpty();

        RuleFor(command => command.Reason)
            .Must(reason => ReportEnums.TryParse<ReportReason>(reason, out _))
            .WithMessage("'{PropertyName}' must be one of: Spam, Harassment, HateSpeech, Violence, Other.");

        RuleFor(command => command.Details).MaximumLength(Report.DetailsMaxLength);
    }
}
