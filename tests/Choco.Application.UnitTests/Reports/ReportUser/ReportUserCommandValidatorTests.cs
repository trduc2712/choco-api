using Choco.Application.Reports.ReportUser;
using Choco.Contract.Reports;
using Choco.Domain.Reports;

using FluentValidation.TestHelper;

namespace Choco.Application.UnitTests.Reports.ReportUser;

public sealed class ReportUserCommandValidatorTests
{
    private static readonly ReportUserCommand ValidCommand = new(
        new Guid("00000000-0000-0000-0000-000000000001"), "Harassment", "details");

    private readonly ReportUserCommandValidator _validator = new();

    [Fact]
    public void Validate_WithValidCommand_HasNoErrors()
    {
        TestValidationResult<ReportUserCommand> result = _validator.TestValidate(ValidCommand);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithoutDetails_HasNoErrors()
    {
        ReportUserCommand command = ValidCommand with { Details = null };

        TestValidationResult<ReportUserCommand> result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithEmptyUserId_HasUserIdError()
    {
        ReportUserCommand command = ValidCommand with { UserId = Guid.Empty };

        TestValidationResult<ReportUserCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.UserId).Only();
    }

    [Theory]
    [InlineData("")]
    [InlineData("harassment")]
    [InlineData("0")]
    [InlineData("Unknown")]
    public void Validate_WithUnknownReason_HasReasonError(string reason)
    {
        ReportUserCommand command = ValidCommand with { Reason = reason };

        TestValidationResult<ReportUserCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.Reason).Only();
    }

    [Fact]
    public void Validate_WithDetailsAtLimit_HasNoErrors()
    {
        ReportUserCommand command = ValidCommand with { Details = new string('a', Report.DetailsMaxLength) };

        TestValidationResult<ReportUserCommand> result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithDetailsPastLimit_HasDetailsError()
    {
        ReportUserCommand command = ValidCommand with { Details = new string('a', Report.DetailsMaxLength + 1) };

        TestValidationResult<ReportUserCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.Details).Only();
    }
}
