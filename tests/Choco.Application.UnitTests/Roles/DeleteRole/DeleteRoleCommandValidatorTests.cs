using Choco.Application.Roles.DeleteRole;
using Choco.Contract.Roles;

using FluentValidation.TestHelper;

namespace Choco.Application.UnitTests.Roles.DeleteRole;

public sealed class DeleteRoleCommandValidatorTests
{
    private readonly DeleteRoleCommandValidator _validator = new();

    [Fact]
    public void Validate_WithId_AcceptsCommand()
    {
        TestValidationResult<DeleteRoleCommand> result = _validator.TestValidate(new DeleteRoleCommand(new Guid("00000000-0000-0000-0000-000000000001")));

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithEmptyId_RejectsId()
    {
        TestValidationResult<DeleteRoleCommand> result = _validator.TestValidate(new DeleteRoleCommand(Guid.Empty));

        result.ShouldHaveValidationErrorFor(c => c.Id).Only();
    }
}
