using Choco.Contract.Feedback;
using Choco.Domain.FeedbackEntries;

using FluentValidation;

namespace Choco.Application.Feedback.GetFeedback;

internal sealed class GetFeedbackQueryValidator : AbstractValidator<GetFeedbackQuery>
{
    public GetFeedbackQueryValidator()
    {
        RuleFor(query => query.Page).GreaterThanOrEqualTo(1);

        RuleFor(query => query.PageSize).InclusiveBetween(1, GetFeedbackQuery.MaxPageSize);

        RuleFor(query => query.Status)
            .Must(status => FeedbackWire.TryParse<FeedbackStatus>(status, out _))
            .When(query => query.Status is not null)
            .WithMessage($"'{{PropertyName}}' must be one of: {FeedbackWire.Options<FeedbackStatus>()}.");

        RuleFor(query => query.Category)
            .Must(category => FeedbackWire.TryParse<FeedbackCategory>(category, out _))
            .When(query => query.Category is not null)
            .WithMessage($"'{{PropertyName}}' must be one of: {FeedbackWire.Options<FeedbackCategory>()}.");
    }
}
