using Choco.Application.Feedback;
using Choco.Application.Feedback.GetFeedback;
using Choco.Contract.Feedback;

using FluentValidation.TestHelper;

namespace Choco.Application.UnitTests.Feedback.GetFeedback;

public sealed class GetFeedbackQueryValidatorTests
{
    private static readonly GetFeedbackQuery ValidQuery = new(null, null, null, GetFeedbackQuery.DefaultLimit);
    private readonly GetFeedbackQueryValidator _validator = new();

    [Fact]
    public void Validate_WithoutFilters_AcceptsQuery()
    {
        var result = _validator.TestValidate(ValidQuery);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithKnownFiltersAndCursor_AcceptsQuery()
    {
        string cursor = new FeedbackCursor(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), Guid.Empty).Encode();

        var result = _validator.TestValidate(new GetFeedbackQuery("in_progress", "suggestion", cursor, 1));

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData("New")]
    [InlineData("InProgress")]
    [InlineData("unknown")]
    public void Validate_WithUnknownStatus_RejectsStatus(string status)
    {
        var result = _validator.TestValidate(ValidQuery with { Status = status });

        result.ShouldHaveValidationErrorFor(query => query.Status).Only();
    }

    [Theory]
    [InlineData("")]
    [InlineData("Bug")]
    [InlineData("unknown")]
    public void Validate_WithUnknownCategory_RejectsCategory(string category)
    {
        var result = _validator.TestValidate(ValidQuery with { Category = category });

        result.ShouldHaveValidationErrorFor(query => query.Category).Only();
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid")]
    public void Validate_WithInvalidCursor_RejectsCursor(string cursor)
    {
        var result = _validator.TestValidate(ValidQuery with { Cursor = cursor });

        result.ShouldHaveValidationErrorFor(query => query.Cursor).Only();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(GetFeedbackQuery.MaxLimit + 1)]
    public void Validate_WithLimitOutOfRange_RejectsLimit(int limit)
    {
        var result = _validator.TestValidate(ValidQuery with { Limit = limit });

        result.ShouldHaveValidationErrorFor(query => query.Limit).Only();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(GetFeedbackQuery.MaxLimit)]
    public void Validate_WithLimitAtBounds_AcceptsLimit(int limit)
    {
        var result = _validator.TestValidate(ValidQuery with { Limit = limit });

        result.ShouldNotHaveAnyValidationErrors();
    }
}
