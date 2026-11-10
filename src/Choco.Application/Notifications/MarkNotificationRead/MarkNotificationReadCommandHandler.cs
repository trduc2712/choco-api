using Choco.Application.Abstractions.Authentication;
using Choco.Application.Abstractions.Messaging;
using Choco.Contract.Notifications;
using Choco.Domain.Notifications;
using Choco.Domain.Primitives.Persistence;
using Choco.SharedKernel.Results;

namespace Choco.Application.Notifications.MarkNotificationRead;

internal sealed class MarkNotificationReadCommandHandler(
    ICurrentUser currentUser,
    INotificationRepository notificationRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : ICommandHandler<MarkNotificationReadCommand>
{
    public async Task<Result> Handle(MarkNotificationReadCommand command, CancellationToken cancellationToken)
    {
        Result<Guid> caller = await currentUser.GetIdAsync(cancellationToken);
        if (caller.IsFailure)
        {
            return caller;
        }

        Notification? notification = await notificationRepository.GetByIdForRecipientAsync(
            command.NotificationId, caller.Value, cancellationToken);
        if (notification is null)
        {
            return Result.Failure(NotificationErrors.NotFound);
        }

        notification.MarkRead(timeProvider.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
