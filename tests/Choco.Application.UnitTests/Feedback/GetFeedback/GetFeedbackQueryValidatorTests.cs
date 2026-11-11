using Choco.Application.Feedback.GetFeedback;
using Choco.Contract.Feedback;

using FluentValidation.TestHelper;

namespace Choco.Application.UnitTests.Feedback.GetFeedback;

public sealed class GetFeedbackQueryValidatorTests
{
    private static readonly GetFeedbackQuery ValidQuery = new(null, null, GetFeedbackQuery.DefaultPage, GetFeedbackQuery.DefaultPageSize);
    private readonly GetFeedbackQueryValidator _validator = new();

    [Fact]
    public void Validate_WithoutFilters_AcceptsQuery()
    {
        var result = _validator.TestValidate(ValidQuery);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithKnownFiltersAndPage_AcceptsQuery()
    {
        var result = _validator.TestValidate(new GetFeedbackQuery("in_progress", "suggestion", 3, 1));

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
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_WithPageBelowOne_RejectsPage(int page)
    {
        var result = _validator.TestValidate(ValidQuery with { Page = page });

        result.ShouldHaveValidationErrorFor(query => query.Page).Only();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(int.MaxValue)]
    public void Validate_WithPageAtOrAboveOne_AcceptsPage(int page)
    {
        var result = _validator.TestValidate(ValidQuery with { Page = page });

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(GetFeedbackQuery.MaxPageSize + 1)]
    public void Validate_WithPageSizeOutOfRange_RejectsPageSize(int pageSize)
    {
        var result = _validator.TestValidate(ValidQuery with { PageSize = pageSize });

        result.ShouldHaveValidationErrorFor(query => query.PageSize).Only();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(GetFeedbackQuery.MaxPageSize)]
    public void Validate_WithPageSizeAtBounds_AcceptsPageSize(int pageSize)
    {
        var result = _validator.TestValidate(ValidQuery with { PageSize = pageSize });

        result.ShouldNotHaveAnyValidationErrors();
    }
}
