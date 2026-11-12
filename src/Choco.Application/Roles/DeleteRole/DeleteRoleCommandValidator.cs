using Choco.Contract.Roles;

using FluentValidation;

namespace Choco.Application.Roles.DeleteRole;

internal sealed class DeleteRoleCommandValidator : AbstractValidator<DeleteRoleCommand>
{
    public DeleteRoleCommandValidator()
    {
        RuleFor(command => command.Id).NotEmpty();
    }
}
