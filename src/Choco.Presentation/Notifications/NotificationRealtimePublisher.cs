using Choco.Application.Abstractions.Notifications;
using Choco.Contract.Notifications;

using Microsoft.AspNetCore.SignalR;

namespace Choco.Presentation.Notifications;

internal sealed class NotificationRealtimePublisher(IHubContext<NotificationsHub> hubContext)
    : INotificationRealtimePublisher
{
    public Task PublishAsync(
        string recipientIdentityId, NotificationResponse notification, CancellationToken cancellationToken)
    {
        return hubContext.Clients.User(recipientIdentityId)
            .SendAsync(NotificationsHub.NotificationReceivedEvent, notification, cancellationToken);
    }
}
