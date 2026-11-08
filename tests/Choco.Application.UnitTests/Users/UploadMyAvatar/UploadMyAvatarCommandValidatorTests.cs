using Choco.Application.Users.UploadMyAvatar;
using Choco.Contract.Users;

using FluentValidation.TestHelper;

namespace Choco.Application.UnitTests.Users.UploadMyAvatar;

public sealed class UploadMyAvatarCommandValidatorTests
{
    private readonly UploadMyAvatarCommandValidator _validator = new();

    [Theory]
    [InlineData(0)]
    [InlineData(UploadMyAvatarCommand.MaxImageBytes + 1)]
    public void Validate_WithImageOutsideLimits_RejectsImage(int length)
    {
        using MemoryStream image = new(new byte[length]);

        var result = _validator.TestValidate(new UploadMyAvatarCommand(image));

        result.ShouldHaveValidationErrorFor(command => command.Image).Only();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(UploadMyAvatarCommand.MaxImageBytes)]
    public void Validate_WithImageWithinLimits_AcceptsImage(int length)
    {
        using MemoryStream image = new(new byte[length]);

        var result = _validator.TestValidate(new UploadMyAvatarCommand(image));

        result.ShouldNotHaveAnyValidationErrors();
    }
}
