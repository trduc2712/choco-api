using Choco.Contract.Users;

using FluentValidation;

namespace Choco.Application.Users.UnfollowUser;

internal sealed class UnfollowUserCommandValidator : AbstractValidator<UnfollowUserCommand>
{
    public UnfollowUserCommandValidator()
    {
        RuleFor(command => command.UserId).NotEmpty();
    }
}
