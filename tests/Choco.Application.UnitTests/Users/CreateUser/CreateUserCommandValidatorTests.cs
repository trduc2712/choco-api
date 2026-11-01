using Choco.Application.Users.CreateUser;
using Choco.Contract.Users;
using Choco.Domain.Users;

using FluentValidation.TestHelper;

namespace Choco.Application.UnitTests.Users.CreateUser;

public sealed class CreateUserCommandValidatorTests
{
    private static readonly CreateUserCommand ValidCommand =
        new("identity-id", "username", "Display Name", "https://example.com/avatar.png");

    private readonly CreateUserCommandValidator _validator = new();

    [Fact]
    public void Validate_WithValidCommand_HasNoErrors()
    {
        TestValidationResult<CreateUserCommand> result = _validator.TestValidate(ValidCommand);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithoutAvatarUrl_HasNoErrors()
    {
        CreateUserCommand command = ValidCommand with { AvatarUrl = null };

        TestValidationResult<CreateUserCommand> result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithEveryFieldAtMaxLength_HasNoErrors()
    {
        CreateUserCommand command = new(
            new string('i', User.IdentityIdMaxLength),
            new string('u', User.UsernameMaxLength),
            new string('d', User.DisplayNameMaxLength),
            "https://example.com/" + new string('a', User.AvatarUrlMaxLength - "https://example.com/".Length));

        TestValidationResult<CreateUserCommand> result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Validate_WithEmptyIdentityId_HasIdentityIdError(string? identityId)
    {
        CreateUserCommand command = ValidCommand with { IdentityId = identityId! };

        TestValidationResult<CreateUserCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.IdentityId).Only();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Validate_WithEmptyUsername_HasUsernameError(string? username)
    {
        CreateUserCommand command = ValidCommand with { Username = username! };

        TestValidationResult<CreateUserCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.Username).Only();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Validate_WithoutDisplayName_HasNoErrors(string? displayName)
    {
        CreateUserCommand command = ValidCommand with { DisplayName = displayName };

        TestValidationResult<CreateUserCommand> result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithTooLongIdentityId_HasIdentityIdError()
    {
        CreateUserCommand command = ValidCommand with { IdentityId = new string('i', User.IdentityIdMaxLength + 1) };

        TestValidationResult<CreateUserCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.IdentityId).Only();
    }

    [Fact]
    public void Validate_WithTooLongUsername_HasUsernameError()
    {
        CreateUserCommand command = ValidCommand with { Username = new string('u', User.UsernameMaxLength + 1) };

        TestValidationResult<CreateUserCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.Username).Only();
    }

    [Fact]
    public void Validate_WithTooLongDisplayName_HasDisplayNameError()
    {
        CreateUserCommand command = ValidCommand with { DisplayName = new string('d', User.DisplayNameMaxLength + 1) };

        TestValidationResult<CreateUserCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.DisplayName).Only();
    }

    [Fact]
    public void Validate_WithTooLongAvatarUrl_HasAvatarUrlError()
    {
        CreateUserCommand command = ValidCommand with { AvatarUrl = new string('a', User.AvatarUrlMaxLength + 1) };

        TestValidationResult<CreateUserCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.AvatarUrl).Only();
    }
}
