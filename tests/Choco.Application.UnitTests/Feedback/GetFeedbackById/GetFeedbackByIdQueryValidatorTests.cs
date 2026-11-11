using Choco.Application.Feedback.GetFeedbackById;
using Choco.Contract.Feedback;

using FluentValidation.TestHelper;

namespace Choco.Application.UnitTests.Feedback.GetFeedbackById;

public sealed class GetFeedbackByIdQueryValidatorTests
{
    private readonly GetFeedbackByIdQueryValidator _validator = new();

    [Fact]
    public void Validate_WithId_AcceptsQuery()
    {
        var result = _validator.TestValidate(new GetFeedbackByIdQuery(new Guid("00000000-0000-0000-0000-000000000001")));

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithEmptyId_RejectsId()
    {
        var result = _validator.TestValidate(new GetFeedbackByIdQuery(Guid.Empty));

        result.ShouldHaveValidationErrorFor(query => query.Id).Only();
    }
}
