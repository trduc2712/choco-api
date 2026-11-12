using Choco.Application.Users.SuspendUser;
using Choco.Contract.Users;
using Choco.Domain.Users;

using FluentValidation.TestHelper;

namespace Choco.Application.UnitTests.Users.SuspendUser;

public sealed class SuspendUserCommandValidatorTests
{
    private static readonly SuspendUserCommand ValidCommand = new(new Guid("00000000-0000-0000-0000-000000000001"), "reason");

    private readonly SuspendUserCommandValidator _validator = new();

    [Fact]
    public void Validate_WithValidCommand_HasNoErrors()
    {
        TestValidationResult<SuspendUserCommand> result = _validator.TestValidate(ValidCommand);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithEmptyUserId_HasUserIdError()
    {
        TestValidationResult<SuspendUserCommand> result = _validator.TestValidate(ValidCommand with { UserId = Guid.Empty });

        result.ShouldHaveValidationErrorFor(command => command.UserId).Only();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Validate_WithBlankReason_HasReasonError(string? reason)
    {
        TestValidationResult<SuspendUserCommand> result = _validator.TestValidate(ValidCommand with { Reason = reason! });

        result.ShouldHaveValidationErrorFor(command => command.Reason).Only();
    }

    [Fact]
    public void Validate_WithReasonAtMaxLengthAfterTrimming_HasNoErrors()
    {
        SuspendUserCommand command = ValidCommand with { Reason = $"  {new string('r', User.SuspensionReasonMaxLength)}  " };

        TestValidationResult<SuspendUserCommand> result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithReasonOverMaxLength_HasReasonError()
    {
        SuspendUserCommand command = ValidCommand with { Reason = new string('r', User.SuspensionReasonMaxLength + 1) };

        TestValidationResult<SuspendUserCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.Reason).Only();
    }
}
