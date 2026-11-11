using Choco.Application.Users.BlockUser;
using Choco.Contract.Users;

using FluentValidation.TestHelper;

namespace Choco.Application.UnitTests.Users.BlockUser;

public sealed class BlockUserCommandValidatorTests
{
    private static readonly BlockUserCommand ValidCommand = new(new Guid("00000000-0000-0000-0000-000000000001"));
    private readonly BlockUserCommandValidator _validator = new();

    [Fact]
    public void Validate_WithValidCommand_HasNoErrors()
    {
        TestValidationResult<BlockUserCommand> result = _validator.TestValidate(ValidCommand);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithEmptyUserId_HasUserIdError()
    {
        BlockUserCommand command = ValidCommand with { UserId = Guid.Empty };

        TestValidationResult<BlockUserCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(input => input.UserId).Only();
    }
}
