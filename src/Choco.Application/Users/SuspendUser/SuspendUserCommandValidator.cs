using Choco.Contract.Users;
using Choco.Domain.Users;

using FluentValidation;

namespace Choco.Application.Users.SuspendUser;

internal sealed class SuspendUserCommandValidator : AbstractValidator<SuspendUserCommand>
{
    public SuspendUserCommandValidator()
    {
        RuleFor(command => command.UserId).NotEmpty();

        RuleFor(command => command.Reason)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .Must(reason => reason.Trim().Length <= User.SuspensionReasonMaxLength)
            .WithErrorCode("TooLong")
            .WithMessage($"'{{PropertyName}}' must be {User.SuspensionReasonMaxLength} characters or fewer once leading and trailing whitespace is removed.");
    }
}
