using Choco.Contract.Users;

using FluentValidation;

namespace Choco.Application.Users.UploadMyAvatar;

internal sealed class UploadMyAvatarCommandValidator : AbstractValidator<UploadMyAvatarCommand>
{
    public UploadMyAvatarCommandValidator()
    {
        RuleFor(command => command.Image)
            .Must(image => image.Length is > 0 and <= UploadMyAvatarCommand.MaxImageBytes)
            .WithErrorCode("OutOfRange")
            .WithMessage($"The image must contain between 1 and {UploadMyAvatarCommand.MaxImageBytes} bytes.");
    }
}
