using Choco.Contract.Users;

using FluentValidation;

namespace Choco.Application.Users.BlockUser;

internal sealed class BlockUserCommandValidator : AbstractValidator<BlockUserCommand>
{
    public BlockUserCommandValidator()
    {
        RuleFor(command => command.UserId).NotEmpty();
    }
}
