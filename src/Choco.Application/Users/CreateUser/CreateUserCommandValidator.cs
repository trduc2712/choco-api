using Choco.Contract.Users;
using Choco.Domain.Users;

using FluentValidation;

namespace Choco.Application.Users.CreateUser;

internal sealed class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserCommandValidator()
    {
        RuleFor(command => command.IdentityId).NotEmpty().MaximumLength(User.IdentityIdMaxLength);
        RuleFor(command => command.Username).NotEmpty().MaximumLength(User.UsernameMaxLength);
        RuleFor(command => command.DisplayName).MaximumLength(User.DisplayNameMaxLength);
        RuleFor(command => command.AvatarUrl).MaximumLength(User.AvatarUrlMaxLength);
    }
}
