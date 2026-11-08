using Choco.Application.Users.UpdateMyProfile;
using Choco.Contract.Users;
using Choco.Domain.Users;

using FluentValidation.TestHelper;

namespace Choco.Application.UnitTests.Users.UpdateMyProfile;

public sealed class UpdateMyProfileCommandValidatorTests
{
    private static readonly UpdateMyProfileCommand ValidCommand = new("username", "First Name", "Last Name");
    private readonly UpdateMyProfileCommandValidator _validator = new();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Validate_WithBlankUsername_RejectsUsername(string? username)
    {
        var result = _validator.TestValidate(ValidCommand with { Username = username! });

        result.ShouldHaveValidationErrorFor(command => command.Username).Only();
    }

    [Fact]
    public void Validate_WithUsernameOverLimit_RejectsUsername()
    {
        var result = _validator.TestValidate(ValidCommand with { Username = new string('u', User.UsernameMaxLength + 1) });

        result.ShouldHaveValidationErrorFor(command => command.Username).Only();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Validate_WithBlankFirstName_RejectsFirstName(string? firstName)
    {
        var result = _validator.TestValidate(ValidCommand with { FirstName = firstName! });

        result.ShouldHaveValidationErrorFor(command => command.FirstName).Only();
    }

    [Fact]
    public void Validate_WithFirstNameOverLimit_RejectsFirstName()
    {
        var result = _validator.TestValidate(ValidCommand with
        {
            FirstName = new string('f', User.DisplayNameMaxLength + 1),
            LastName = null
        });

        result.ShouldHaveValidationErrorFor(command => command.FirstName).Only();
    }

    [Fact]
    public void Validate_WithCombinedNameOverLimit_RejectsLastName()
    {
        var result = _validator.TestValidate(ValidCommand with
        {
            FirstName = new string('f', User.DisplayNameMaxLength - 1),
            LastName = "l"
        });

        result.ShouldHaveValidationErrorFor(command => command.LastName).Only();
    }

    [Fact]
    public void Validate_WithFieldsAtLimits_AcceptsProfile()
    {
        var result = _validator.TestValidate(ValidCommand with
        {
            Username = new string('u', User.UsernameMaxLength),
            FirstName = new string('f', User.DisplayNameMaxLength - 2),
            LastName = "l"
        });

        result.ShouldNotHaveAnyValidationErrors();
    }
}
