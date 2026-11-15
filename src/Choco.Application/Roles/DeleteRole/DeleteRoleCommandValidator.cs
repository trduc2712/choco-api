using Choco.Contract.Roles;
using Choco.Domain.AuditLogs;

using FluentValidation;

namespace Choco.Application.Roles.DeleteRole;

internal sealed class DeleteRoleCommandValidator : AbstractValidator<DeleteRoleCommand>
{
    public DeleteRoleCommandValidator()
    {
        RuleFor(command => command.Id).NotEmpty();
        RuleFor(command => command.Reason).MaximumLength(AuditLogEntry.ReasonMaxLength);
    }
}
