using Choco.Contract.Notifications;

namespace Choco.Application.Abstractions.Notifications;

public interface INotificationRealtimePublisher
{
    Task PublishAsync(string recipientIdentityId, NotificationResponse notification, CancellationToken cancellationToken);
}
