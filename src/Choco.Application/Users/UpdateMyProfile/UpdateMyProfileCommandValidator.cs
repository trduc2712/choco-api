using Choco.Contract.Users;
using Choco.Domain.Users;

using FluentValidation;

namespace Choco.Application.Users.UpdateMyProfile;

internal sealed class UpdateMyProfileCommandValidator : AbstractValidator<UpdateMyProfileCommand>
{
    public UpdateMyProfileCommandValidator()
    {
        RuleFor(command => command.Username).NotEmpty().MaximumLength(User.UsernameMaxLength);
        RuleFor(command => command.FirstName).NotEmpty().MaximumLength(User.DisplayNameMaxLength);
        RuleFor(command => command.LastName)
            .Must((command, lastName) => string.IsNullOrWhiteSpace(lastName)
                || (command.FirstName?.Trim().Length ?? 0) + 1 + lastName.Trim().Length <= User.DisplayNameMaxLength)
            .WithErrorCode("TooLong")
            .WithMessage($"The full display name must be at most {User.DisplayNameMaxLength} characters.");
    }
}
