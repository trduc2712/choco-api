using Choco.Application.Users.UnblockUser;
using Choco.Contract.Users;

using FluentValidation.TestHelper;

namespace Choco.Application.UnitTests.Users.UnblockUser;

public sealed class UnblockUserCommandValidatorTests
{
    private static readonly UnblockUserCommand ValidCommand = new(new Guid("00000000-0000-0000-0000-000000000001"));
    private readonly UnblockUserCommandValidator _validator = new();

    [Fact]
    public void Validate_WithValidCommand_HasNoErrors()
    {
        TestValidationResult<UnblockUserCommand> result = _validator.TestValidate(ValidCommand);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithEmptyUserId_HasUserIdError()
    {
        UnblockUserCommand command = ValidCommand with { UserId = Guid.Empty };

        TestValidationResult<UnblockUserCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(input => input.UserId).Only();
    }
}
