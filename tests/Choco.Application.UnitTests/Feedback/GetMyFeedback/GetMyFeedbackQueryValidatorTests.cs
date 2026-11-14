using Choco.Application.Feedback;
using Choco.Application.Feedback.GetMyFeedback;
using Choco.Contract.Feedback;

using FluentValidation.TestHelper;

namespace Choco.Application.UnitTests.Feedback.GetMyFeedback;

public sealed class GetMyFeedbackQueryValidatorTests
{
    private static readonly GetMyFeedbackQuery ValidQuery = new(null, GetMyFeedbackQuery.DefaultLimit);

    private readonly GetMyFeedbackQueryValidator _validator = new();

    [Fact]
    public void Validate_WithoutCursor_HasNoErrors()
    {
        TestValidationResult<GetMyFeedbackQuery> result = _validator.TestValidate(ValidQuery);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithValidCursor_HasNoErrors()
    {
        string cursor = new FeedbackCursor(
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new Guid("3c4d5e6f-7a8b-4c9d-8e0f-1a2b3c4d5e6f")).Encode();
        GetMyFeedbackQuery query = ValidQuery with { Cursor = cursor };

        TestValidationResult<GetMyFeedbackQuery> result = _validator.TestValidate(query);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(GetMyFeedbackQuery.MaxLimit)]
    public void Validate_WithLimitAtBound_HasNoErrors(int limit)
    {
        GetMyFeedbackQuery query = ValidQuery with { Limit = limit };

        TestValidationResult<GetMyFeedbackQuery> result = _validator.TestValidate(query);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(GetMyFeedbackQuery.MaxLimit + 1)]
    public void Validate_WithLimitOutOfRange_HasLimitError(int limit)
    {
        GetMyFeedbackQuery query = ValidQuery with { Limit = limit };

        TestValidationResult<GetMyFeedbackQuery> result = _validator.TestValidate(query);

        result.ShouldHaveValidationErrorFor(q => q.Limit).Only();
    }

    [Theory]
    [InlineData("")]
    [InlineData("not a cursor")]
    public void Validate_WithInvalidCursor_HasCursorError(string cursor)
    {
        GetMyFeedbackQuery query = ValidQuery with { Cursor = cursor };

        TestValidationResult<GetMyFeedbackQuery> result = _validator.TestValidate(query);

        result.ShouldHaveValidationErrorFor(q => q.Cursor).Only();
    }
}
