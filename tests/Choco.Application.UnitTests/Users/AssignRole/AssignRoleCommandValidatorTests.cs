using Choco.Application.Users.AssignRole;
using Choco.Contract.Users;

using FluentValidation.TestHelper;

namespace Choco.Application.UnitTests.Users.AssignRole;

public sealed class AssignRoleCommandValidatorTests
{
    private static readonly AssignRoleCommand ValidCommand = new(
        new Guid("1a2b3c4d-5e6f-4a7b-8c9d-0e1f2a3b4c5d"),
        new Guid("2b3c4d5e-6f7a-4b8c-9d0e-1f2a3b4c5d6e"));

    private readonly AssignRoleCommandValidator _validator = new();

    [Fact]
    public void Validate_WithValidCommand_HasNoErrors()
    {
        TestValidationResult<AssignRoleCommand> result = _validator.TestValidate(ValidCommand);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithEmptyUserId_HasUserIdError()
    {
        AssignRoleCommand command = ValidCommand with { UserId = Guid.Empty };

        TestValidationResult<AssignRoleCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.UserId).Only();
    }

    [Fact]
    public void Validate_WithEmptyRoleId_HasRoleIdError()
    {
        AssignRoleCommand command = ValidCommand with { RoleId = Guid.Empty };

        TestValidationResult<AssignRoleCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.RoleId).Only();
    }
}
