using Choco.Contract.Feedback;

using FluentValidation;

namespace Choco.Application.Feedback.GetFeedbackById;

internal sealed class GetFeedbackByIdQueryValidator : AbstractValidator<GetFeedbackByIdQuery>
{
    public GetFeedbackByIdQueryValidator()
    {
        RuleFor(query => query.Id).NotEmpty();
    }
}
