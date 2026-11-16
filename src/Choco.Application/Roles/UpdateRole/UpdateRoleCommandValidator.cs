using Choco.Contract.Roles;
using Choco.Domain.AuditLogs;
using Choco.Domain.Roles;

using FluentValidation;

namespace Choco.Application.Roles.UpdateRole;

internal sealed class UpdateRoleCommandValidator : AbstractValidator<UpdateRoleCommand>
{
    public UpdateRoleCommandValidator()
    {
        RuleFor(command => command.Id).NotEmpty();
        RuleFor(command => command.Reason).MaximumLength(AuditLogEntry.ReasonMaxLength);

        RuleFor(command => command.Name)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .Must(name => name.Trim().Length <= Role.NameMaxLength)
            .WithErrorCode("TooLong")
            .WithMessage($"'{{PropertyName}}' must be {Role.NameMaxLength} characters or fewer once leading and trailing whitespace is removed.");

        RuleFor(command => command.Description)
            .Must(description => description is null || description.Trim().Length <= Role.DescriptionMaxLength)
            .WithErrorCode("TooLong")
            .WithMessage($"'{{PropertyName}}' must be {Role.DescriptionMaxLength} characters or fewer once trimmed.");

        RuleFor(command => command.Color)
            .Must(color => color is not null && RoleColors.All.Contains(color))
            .WithErrorCode("UnknownRoleColor")
            .WithMessage("'{PropertyName}' must be a supported color key.");

        RuleFor(command => command.Icon)
            .Must(icon => icon is not null && RoleIcons.All.Contains(icon))
            .WithErrorCode("UnknownRoleIcon")
            .WithMessage("'{PropertyName}' must be a supported icon key.");

        RuleFor(command => command.Permissions)
            .Cascade(CascadeMode.Stop)
            .NotNull()
            .Must(permissions => permissions.All(permission => permission is not null && Permission.All.Contains(permission)))
            .WithErrorCode("UnknownPermission")
            .WithMessage("'{PropertyName}' must only contain permissions from the catalog.");
    }
}
