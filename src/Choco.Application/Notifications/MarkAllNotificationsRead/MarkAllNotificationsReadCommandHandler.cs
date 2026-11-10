using Choco.Application.Abstractions.Authentication;
using Choco.Application.Abstractions.Messaging;
using Choco.Contract.Notifications;
using Choco.Domain.Notifications;
using Choco.SharedKernel.Results;

namespace Choco.Application.Notifications.MarkAllNotificationsRead;

internal sealed class MarkAllNotificationsReadCommandHandler(
    ICurrentUser currentUser,
    INotificationRepository notificationRepository,
    TimeProvider timeProvider)
    : ICommandHandler<MarkAllNotificationsReadCommand>
{
    public async Task<Result> Handle(MarkAllNotificationsReadCommand command, CancellationToken cancellationToken)
    {
        Result<Guid> caller = await currentUser.GetIdAsync(cancellationToken);
        if (caller.IsFailure)
        {
            return caller;
        }

        await notificationRepository.MarkAllReadAsync(caller.Value, timeProvider.GetUtcNow(), cancellationToken);

        return Result.Success();
    }
}
