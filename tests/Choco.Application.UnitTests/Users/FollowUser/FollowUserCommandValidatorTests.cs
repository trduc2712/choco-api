using Choco.Application.Users.FollowUser;
using Choco.Contract.Users;

using FluentValidation.TestHelper;

namespace Choco.Application.UnitTests.Users.FollowUser;

public sealed class FollowUserCommandValidatorTests
{
    private static readonly FollowUserCommand ValidCommand = new(new Guid("00000000-0000-0000-0000-000000000001"));
    private readonly FollowUserCommandValidator _validator = new();

    [Fact]
    public void Validate_WithValidCommand_HasNoErrors()
    {
        TestValidationResult<FollowUserCommand> result = _validator.TestValidate(ValidCommand);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithEmptyUserId_HasUserIdError()
    {
        FollowUserCommand command = ValidCommand with { UserId = Guid.Empty };

        TestValidationResult<FollowUserCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(input => input.UserId).Only();
    }
}
