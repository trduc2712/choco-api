using Choco.Application.Users.UnfollowUser;
using Choco.Contract.Users;

using FluentValidation.TestHelper;

namespace Choco.Application.UnitTests.Users.UnfollowUser;

public sealed class UnfollowUserCommandValidatorTests
{
    private static readonly UnfollowUserCommand ValidCommand = new(new Guid("00000000-0000-0000-0000-000000000001"));
    private readonly UnfollowUserCommandValidator _validator = new();

    [Fact]
    public void Validate_WithValidCommand_HasNoErrors()
    {
        TestValidationResult<UnfollowUserCommand> result = _validator.TestValidate(ValidCommand);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithEmptyUserId_HasUserIdError()
    {
        UnfollowUserCommand command = ValidCommand with { UserId = Guid.Empty };

        TestValidationResult<UnfollowUserCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(input => input.UserId).Only();
    }
}
