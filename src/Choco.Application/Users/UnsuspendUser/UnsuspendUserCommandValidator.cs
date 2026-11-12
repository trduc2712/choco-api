using Choco.Contract.Users;

using FluentValidation;

namespace Choco.Application.Users.UnsuspendUser;

internal sealed class UnsuspendUserCommandValidator : AbstractValidator<UnsuspendUserCommand>
{
    public UnsuspendUserCommandValidator()
    {
        RuleFor(command => command.UserId).NotEmpty();
    }
}
