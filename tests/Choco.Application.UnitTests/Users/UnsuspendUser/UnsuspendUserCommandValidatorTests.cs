using Choco.Application.Users.UnsuspendUser;
using Choco.Contract.Users;

using FluentValidation.TestHelper;

namespace Choco.Application.UnitTests.Users.UnsuspendUser;

public sealed class UnsuspendUserCommandValidatorTests
{
    private readonly UnsuspendUserCommandValidator _validator = new();

    [Fact]
    public void Validate_WithUserId_AcceptsCommand()
    {
        TestValidationResult<UnsuspendUserCommand> result = _validator.TestValidate(
            new UnsuspendUserCommand(new Guid("00000000-0000-0000-0000-000000000001")));

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithEmptyUserId_RejectsUserId()
    {
        TestValidationResult<UnsuspendUserCommand> result = _validator.TestValidate(new UnsuspendUserCommand(Guid.Empty));

        result.ShouldHaveValidationErrorFor(command => command.UserId).Only();
    }
}
