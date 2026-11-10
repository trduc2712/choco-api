using Choco.Contract.Notifications;

using FluentValidation;

namespace Choco.Application.Notifications.MarkNotificationRead;

internal sealed class MarkNotificationReadCommandValidator : AbstractValidator<MarkNotificationReadCommand>
{
    public MarkNotificationReadCommandValidator()
    {
        RuleFor(command => command.NotificationId).NotEmpty();
    }
}
