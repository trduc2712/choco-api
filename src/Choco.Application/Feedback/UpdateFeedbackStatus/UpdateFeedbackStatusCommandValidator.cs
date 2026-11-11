using Choco.Contract.Feedback;
using Choco.Domain.FeedbackEntries;

using FluentValidation;

namespace Choco.Application.Feedback.UpdateFeedbackStatus;

internal sealed class UpdateFeedbackStatusCommandValidator : AbstractValidator<UpdateFeedbackStatusCommand>
{
    public UpdateFeedbackStatusCommandValidator()
    {
        RuleFor(command => command.Id).NotEmpty();

        RuleFor(command => command.Status)
            .Must(status => FeedbackWire.TryParse<FeedbackStatus>(status, out _))
            .WithMessage($"'{{PropertyName}}' must be one of: {FeedbackWire.Options<FeedbackStatus>()}.");
    }
}
