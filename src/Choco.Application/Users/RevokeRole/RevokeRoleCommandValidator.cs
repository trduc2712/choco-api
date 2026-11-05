using Choco.Contract.Users;

using FluentValidation;

namespace Choco.Application.Users.RevokeRole;

internal sealed class RevokeRoleCommandValidator : AbstractValidator<RevokeRoleCommand>
{
    public RevokeRoleCommandValidator()
    {
        RuleFor(command => command.UserId).NotEmpty();
        RuleFor(command => command.RoleId).NotEmpty();
    }
}
