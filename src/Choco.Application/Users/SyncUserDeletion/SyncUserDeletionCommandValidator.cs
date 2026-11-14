using Choco.Contract.Users;
using Choco.Domain.Users;

using FluentValidation;

namespace Choco.Application.Users.SyncUserDeletion;

internal sealed class SyncUserDeletionCommandValidator : AbstractValidator<SyncUserDeletionCommand>
{
    public SyncUserDeletionCommandValidator()
    {
        RuleFor(command => command.IdentityId).NotEmpty().MaximumLength(User.IdentityIdMaxLength);
    }
}
