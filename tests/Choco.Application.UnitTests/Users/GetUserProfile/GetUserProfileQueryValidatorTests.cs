using Choco.Application.Users.GetUserProfile;
using Choco.Contract.Users;

using FluentValidation.TestHelper;

namespace Choco.Application.UnitTests.Users.GetUserProfile;

public sealed class GetUserProfileQueryValidatorTests
{
    private static readonly GetUserProfileQuery ValidQuery = new(new Guid("00000000-0000-0000-0000-000000000001"));

    private readonly GetUserProfileQueryValidator _validator = new();

    [Fact]
    public void Validate_WithValidQuery_HasNoErrors()
    {
        TestValidationResult<GetUserProfileQuery> result = _validator.TestValidate(ValidQuery);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithEmptyId_HasIdError()
    {
        GetUserProfileQuery query = ValidQuery with { Id = Guid.Empty };

        TestValidationResult<GetUserProfileQuery> result = _validator.TestValidate(query);

        result.ShouldHaveValidationErrorFor(q => q.Id).Only();
    }
}
