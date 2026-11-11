using Choco.Contract.Users;

using FluentValidation;

namespace Choco.Application.Users.UnblockUser;

internal sealed class UnblockUserCommandValidator : AbstractValidator<UnblockUserCommand>
{
    public UnblockUserCommandValidator()
    {
        RuleFor(command => command.UserId).NotEmpty();
    }
}
