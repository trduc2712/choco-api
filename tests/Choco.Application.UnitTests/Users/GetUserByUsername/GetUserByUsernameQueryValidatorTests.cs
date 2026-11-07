using Choco.Application.Users.GetUserByUsername;
using Choco.Contract.Users;
using Choco.Domain.Users;

using FluentValidation.TestHelper;

namespace Choco.Application.UnitTests.Users.GetUserByUsername;

public sealed class GetUserByUsernameQueryValidatorTests
{
    private static readonly GetUserByUsernameQuery ValidQuery = new("username");

    private readonly GetUserByUsernameQueryValidator _validator = new();

    [Fact]
    public void Validate_WithValidQuery_HasNoErrors()
    {
        TestValidationResult<GetUserByUsernameQuery> result = _validator.TestValidate(ValidQuery);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Validate_WithBlankUsername_HasUsernameError(string? username)
    {
        GetUserByUsernameQuery query = ValidQuery with { Username = username! };

        TestValidationResult<GetUserByUsernameQuery> result = _validator.TestValidate(query);

        result.ShouldHaveValidationErrorFor(q => q.Username).Only();
    }

    [Fact]
    public void Validate_WithUsernameAtMaxLength_HasNoErrors()
    {
        GetUserByUsernameQuery query = ValidQuery with { Username = new string('u', User.UsernameMaxLength) };

        TestValidationResult<GetUserByUsernameQuery> result = _validator.TestValidate(query);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithUsernameOverMaxLength_HasUsernameError()
    {
        GetUserByUsernameQuery query = ValidQuery with { Username = new string('u', User.UsernameMaxLength + 1) };

        TestValidationResult<GetUserByUsernameQuery> result = _validator.TestValidate(query);

        result.ShouldHaveValidationErrorFor(q => q.Username).Only();
    }
}
