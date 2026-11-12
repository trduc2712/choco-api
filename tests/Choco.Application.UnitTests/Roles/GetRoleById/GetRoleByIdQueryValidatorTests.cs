using Choco.Application.Roles.GetRoleById;
using Choco.Contract.Roles;

using FluentValidation.TestHelper;

namespace Choco.Application.UnitTests.Roles.GetRoleById;

public sealed class GetRoleByIdQueryValidatorTests
{
    private readonly GetRoleByIdQueryValidator _validator = new();

    [Fact]
    public void Validate_WithId_AcceptsQuery()
    {
        var result = _validator.TestValidate(new GetRoleByIdQuery(new Guid("00000000-0000-0000-0000-000000000001")));

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithEmptyId_RejectsId()
    {
        var result = _validator.TestValidate(new GetRoleByIdQuery(Guid.Empty));

        result.ShouldHaveValidationErrorFor(query => query.Id).Only();
    }
}
