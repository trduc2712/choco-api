using Choco.Application.Reports.ResolveReport;
using Choco.Contract.Reports;

using FluentValidation.TestHelper;

namespace Choco.Application.UnitTests.Reports.ResolveReport;

public sealed class ResolveReportCommandValidatorTests
{
    private static readonly ResolveReportCommand ValidCommand = new(
        new Guid("00000000-0000-0000-0000-000000000001"), "Dismissed");

    private readonly ResolveReportCommandValidator _validator = new();

    [Theory]
    [InlineData("Dismissed")]
    [InlineData("ActionTaken")]
    public void Validate_WithKnownOutcome_HasNoErrors(string outcome)
    {
        ResolveReportCommand command = ValidCommand with { Outcome = outcome };

        TestValidationResult<ResolveReportCommand> result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithEmptyReportId_HasReportIdError()
    {
        ResolveReportCommand command = ValidCommand with { ReportId = Guid.Empty };

        TestValidationResult<ResolveReportCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.ReportId).Only();
    }

    [Theory]
    [InlineData("")]
    [InlineData("dismissed")]
    [InlineData("1")]
    [InlineData("Unknown")]
    public void Validate_WithUnknownOutcome_HasOutcomeError(string outcome)
    {
        ResolveReportCommand command = ValidCommand with { Outcome = outcome };

        TestValidationResult<ResolveReportCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.Outcome).Only();
    }
}
