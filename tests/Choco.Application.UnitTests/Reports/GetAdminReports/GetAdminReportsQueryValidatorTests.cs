using Choco.Application.Reports.GetAdminReports;
using Choco.Contract.Reports;

using FluentValidation.TestHelper;

namespace Choco.Application.UnitTests.Reports.GetAdminReports;

public sealed class GetAdminReportsQueryValidatorTests
{
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly GetAdminReportsQuery ValidQuery = new(
        null, null, null, null, null, GetAdminReportsQuery.DefaultPage, GetAdminReportsQuery.DefaultPageSize);

    private readonly GetAdminReportsQueryValidator _validator = new();

    [Fact]
    public void Validate_WithDefaults_HasNoErrors()
    {
        TestValidationResult<GetAdminReportsQuery> result = _validator.TestValidate(ValidQuery);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("needle")]
    public void Validate_WithBlankOrShortSearch_HasNoErrors(string? text)
    {
        GetAdminReportsQuery query = ValidQuery with { Q = text };

        TestValidationResult<GetAdminReportsQuery> result = _validator.TestValidate(query);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithSearchAtTheLimitAfterTrimming_HasNoErrors()
    {
        GetAdminReportsQuery query = ValidQuery with { Q = $"  {new string('q', GetAdminReportsQuery.SearchMaxLength)}  " };

        TestValidationResult<GetAdminReportsQuery> result = _validator.TestValidate(query);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithSearchOverTheLimitAfterTrimming_HasSearchError()
    {
        GetAdminReportsQuery query = ValidQuery with { Q = new string('q', GetAdminReportsQuery.SearchMaxLength + 1) };

        TestValidationResult<GetAdminReportsQuery> result = _validator.TestValidate(query);

        result.ShouldHaveValidationErrorFor(q => q.Q).WithErrorCode("TooLong").Only();
    }

    [Fact]
    public void Validate_WithEndAfterStart_HasNoErrors()
    {
        GetAdminReportsQuery query = ValidQuery with { From = Start, To = Start.AddTicks(1) };

        TestValidationResult<GetAdminReportsQuery> result = _validator.TestValidate(query);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_WithEndNotAfterStart_HasEndError(int hours)
    {
        GetAdminReportsQuery query = ValidQuery with { From = Start, To = Start.AddHours(hours) };

        TestValidationResult<GetAdminReportsQuery> result = _validator.TestValidate(query);

        result.ShouldHaveValidationErrorFor(q => q.To).Only();
    }

    [Fact]
    public void Validate_WithOnlyOneLimit_HasNoErrors()
    {
        TestValidationResult<GetAdminReportsQuery> onlyStart = _validator.TestValidate(ValidQuery with { From = Start });
        TestValidationResult<GetAdminReportsQuery> onlyEnd = _validator.TestValidate(ValidQuery with { To = Start });

        onlyStart.ShouldNotHaveAnyValidationErrors();
        onlyEnd.ShouldNotHaveAnyValidationErrors();
    }
}
