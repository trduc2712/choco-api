using Choco.Application.Reports.GetReports;
using Choco.Contract.Reports;

using FluentValidation.TestHelper;

namespace Choco.Application.UnitTests.Reports.GetReports;

public sealed class GetReportsQueryValidatorTests
{
    private static readonly GetReportsQuery ValidQuery = new(null, null, GetReportsQuery.DefaultPage, GetReportsQuery.DefaultPageSize);

    private readonly GetReportsQueryValidator _validator = new();

    [Fact]
    public void Validate_WithDefaults_HasNoErrors()
    {
        TestValidationResult<GetReportsQuery> result = _validator.TestValidate(ValidQuery);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(int.MaxValue)]
    public void Validate_WithPageAtOrAboveOne_HasNoErrors(int page)
    {
        GetReportsQuery query = ValidQuery with { Page = page };

        TestValidationResult<GetReportsQuery> result = _validator.TestValidate(query);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_WithPageBelowOne_HasPageError(int page)
    {
        GetReportsQuery query = ValidQuery with { Page = page };

        TestValidationResult<GetReportsQuery> result = _validator.TestValidate(query);

        result.ShouldHaveValidationErrorFor(q => q.Page).Only();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(GetReportsQuery.MaxPageSize)]
    public void Validate_WithPageSizeAtBound_HasNoErrors(int pageSize)
    {
        GetReportsQuery query = ValidQuery with { PageSize = pageSize };

        TestValidationResult<GetReportsQuery> result = _validator.TestValidate(query);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(GetReportsQuery.MaxPageSize + 1)]
    public void Validate_WithPageSizeOutOfRange_HasPageSizeError(int pageSize)
    {
        GetReportsQuery query = ValidQuery with { PageSize = pageSize };

        TestValidationResult<GetReportsQuery> result = _validator.TestValidate(query);

        result.ShouldHaveValidationErrorFor(q => q.PageSize).Only();
    }

    [Theory]
    [InlineData("Post")]
    [InlineData("User")]
    public void Validate_WithKnownTargetType_HasNoErrors(string targetType)
    {
        GetReportsQuery query = ValidQuery with { TargetType = targetType };

        TestValidationResult<GetReportsQuery> result = _validator.TestValidate(query);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData("post")]
    [InlineData("Comment")]
    public void Validate_WithUnknownTargetType_HasTargetTypeError(string targetType)
    {
        GetReportsQuery query = ValidQuery with { TargetType = targetType };

        TestValidationResult<GetReportsQuery> result = _validator.TestValidate(query);

        result.ShouldHaveValidationErrorFor(q => q.TargetType).Only();
    }

    [Theory]
    [InlineData("Spam")]
    [InlineData("Harassment")]
    [InlineData("HateSpeech")]
    [InlineData("Violence")]
    [InlineData("Other")]
    public void Validate_WithKnownReason_HasNoErrors(string reason)
    {
        GetReportsQuery query = ValidQuery with { Reason = reason };

        TestValidationResult<GetReportsQuery> result = _validator.TestValidate(query);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData("spam")]
    [InlineData("Unknown")]
    public void Validate_WithUnknownReason_HasReasonError(string reason)
    {
        GetReportsQuery query = ValidQuery with { Reason = reason };

        TestValidationResult<GetReportsQuery> result = _validator.TestValidate(query);

        result.ShouldHaveValidationErrorFor(q => q.Reason).Only();
    }
}
