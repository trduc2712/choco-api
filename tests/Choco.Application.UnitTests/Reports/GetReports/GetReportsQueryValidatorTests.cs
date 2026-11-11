using Choco.Application.Reports.GetReports;
using Choco.Contract.Reports;

using FluentValidation.TestHelper;

namespace Choco.Application.UnitTests.Reports.GetReports;

public sealed class GetReportsQueryValidatorTests
{
    private static readonly GetReportsQuery ValidQuery = new(GetReportsQuery.DefaultPage, GetReportsQuery.DefaultPageSize);

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
}
