using Choco.Contract.Users;

using FluentValidation;

namespace Choco.Application.Users.FollowUser;

internal sealed class FollowUserCommandValidator : AbstractValidator<FollowUserCommand>
{
    public FollowUserCommandValidator()
    {
        RuleFor(command => command.UserId).NotEmpty();
    }
}
