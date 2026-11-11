using Choco.Application.Users.UpdateMyBio;
using Choco.Contract.Users;
using Choco.Domain.Users;

using FluentValidation.TestHelper;

namespace Choco.Application.UnitTests.Users.UpdateMyBio;

public sealed class UpdateMyBioCommandValidatorTests
{
    private readonly UpdateMyBioCommandValidator _validator = new();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Bio")]
    public void Validate_WithClearedOrShortBio_AcceptsBio(string? bio)
    {
        var result = _validator.TestValidate(new UpdateMyBioCommand(bio));

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithBioAtLimit_AcceptsBio()
    {
        var result = _validator.TestValidate(new UpdateMyBioCommand(new string('b', User.BioMaxLength)));

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithBioOverLimit_RejectsBio()
    {
        var result = _validator.TestValidate(new UpdateMyBioCommand(new string('b', User.BioMaxLength + 1)));

        result.ShouldHaveValidationErrorFor(command => command.Bio).Only();
    }

    [Fact]
    public void Validate_WithBioAtLimitAfterTrimming_AcceptsBio()
    {
        var result = _validator.TestValidate(new UpdateMyBioCommand($"  {new string('b', User.BioMaxLength)}  "));

        result.ShouldNotHaveAnyValidationErrors();
    }
}
