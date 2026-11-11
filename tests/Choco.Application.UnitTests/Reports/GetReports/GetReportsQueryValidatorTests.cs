using Choco.Application.Reports;
using Choco.Application.Reports.GetReports;
using Choco.Contract.Reports;

using FluentValidation.TestHelper;

namespace Choco.Application.UnitTests.Reports.GetReports;

public sealed class GetReportsQueryValidatorTests
{
    private static readonly GetReportsQuery ValidQuery = new(null, GetReportsQuery.DefaultLimit);

    private readonly GetReportsQueryValidator _validator = new();

    [Fact]
    public void Validate_WithoutCursor_HasNoErrors()
    {
        TestValidationResult<GetReportsQuery> result = _validator.TestValidate(ValidQuery);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithValidCursor_HasNoErrors()
    {
        string cursor = new ReportCursor(
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new Guid("3c4d5e6f-7a8b-4c9d-8e0f-1a2b3c4d5e6f")).Encode();
        GetReportsQuery query = ValidQuery with { Cursor = cursor };

        TestValidationResult<GetReportsQuery> result = _validator.TestValidate(query);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(GetReportsQuery.MaxLimit)]
    public void Validate_WithLimitAtBound_HasNoErrors(int limit)
    {
        GetReportsQuery query = ValidQuery with { Limit = limit };

        TestValidationResult<GetReportsQuery> result = _validator.TestValidate(query);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(GetReportsQuery.MaxLimit + 1)]
    public void Validate_WithLimitOutOfRange_HasLimitError(int limit)
    {
        GetReportsQuery query = ValidQuery with { Limit = limit };

        TestValidationResult<GetReportsQuery> result = _validator.TestValidate(query);

        result.ShouldHaveValidationErrorFor(q => q.Limit).Only();
    }

    [Theory]
    [InlineData("")]
    [InlineData("not a cursor")]
    public void Validate_WithInvalidCursor_HasCursorError(string cursor)
    {
        GetReportsQuery query = ValidQuery with { Cursor = cursor };

        TestValidationResult<GetReportsQuery> result = _validator.TestValidate(query);

        result.ShouldHaveValidationErrorFor(q => q.Cursor).Only();
    }
}
