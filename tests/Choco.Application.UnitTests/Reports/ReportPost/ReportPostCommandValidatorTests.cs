using Choco.Application.Reports.ReportPost;
using Choco.Contract.Reports;
using Choco.Domain.Reports;

using FluentValidation.TestHelper;

namespace Choco.Application.UnitTests.Reports.ReportPost;

public sealed class ReportPostCommandValidatorTests
{
    private static readonly ReportPostCommand ValidCommand = new(
        new Guid("00000000-0000-0000-0000-000000000001"), "Spam", "details");

    private readonly ReportPostCommandValidator _validator = new();

    [Fact]
    public void Validate_WithValidCommand_HasNoErrors()
    {
        TestValidationResult<ReportPostCommand> result = _validator.TestValidate(ValidCommand);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithoutDetails_HasNoErrors()
    {
        ReportPostCommand command = ValidCommand with { Details = null };

        TestValidationResult<ReportPostCommand> result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithEmptyPostId_HasPostIdError()
    {
        ReportPostCommand command = ValidCommand with { PostId = Guid.Empty };

        TestValidationResult<ReportPostCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.PostId).Only();
    }

    [Theory]
    [InlineData("")]
    [InlineData("spam")]
    [InlineData("0")]
    [InlineData("Unknown")]
    public void Validate_WithUnknownReason_HasReasonError(string reason)
    {
        ReportPostCommand command = ValidCommand with { Reason = reason };

        TestValidationResult<ReportPostCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.Reason).Only();
    }

    [Fact]
    public void Validate_WithDetailsAtLimit_HasNoErrors()
    {
        ReportPostCommand command = ValidCommand with { Details = new string('a', Report.DetailsMaxLength) };

        TestValidationResult<ReportPostCommand> result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithDetailsPastLimit_HasDetailsError()
    {
        ReportPostCommand command = ValidCommand with { Details = new string('a', Report.DetailsMaxLength + 1) };

        TestValidationResult<ReportPostCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.Details).Only();
    }
}
