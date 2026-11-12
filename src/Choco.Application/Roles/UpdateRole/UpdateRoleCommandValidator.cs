using Choco.Contract.Roles;
using Choco.Domain.Roles;

using FluentValidation;

namespace Choco.Application.Roles.UpdateRole;

internal sealed class UpdateRoleCommandValidator : AbstractValidator<UpdateRoleCommand>
{
    public UpdateRoleCommandValidator()
    {
        RuleFor(command => command.Id).NotEmpty();

        RuleFor(command => command.Name)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .Must(name => name.Trim().Length <= Role.NameMaxLength)
            .WithErrorCode("TooLong")
            .WithMessage($"'{{PropertyName}}' must be {Role.NameMaxLength} characters or fewer once leading and trailing whitespace is removed.");

        RuleFor(command => command.Permissions)
            .Cascade(CascadeMode.Stop)
            .NotNull()
            .Must(permissions => permissions.All(permission => permission is not null && Permission.All.Contains(permission)))
            .WithErrorCode("UnknownPermission")
            .WithMessage("'{PropertyName}' must only contain permissions from the catalog.");
    }
}
