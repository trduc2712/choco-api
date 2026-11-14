using Choco.Contract.Feedback;

using FluentValidation;

namespace Choco.Application.Feedback.GetMyFeedback;

internal sealed class GetMyFeedbackQueryValidator : AbstractValidator<GetMyFeedbackQuery>
{
    public GetMyFeedbackQueryValidator()
    {
        RuleFor(query => query.Limit).InclusiveBetween(1, GetMyFeedbackQuery.MaxLimit);

        RuleFor(query => query.Cursor)
            .Must(cursor => FeedbackCursor.TryDecode(cursor, out _))
            .When(query => query.Cursor is not null)
            .WithMessage("'{PropertyName}' is not a valid cursor.");
    }
}
