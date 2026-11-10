using Choco.SharedKernel.Results;

namespace Choco.Domain.Notifications;

public static class NotificationErrors
{
    public static readonly Error NotFound = Error.NotFound("Notification.NotFound", "The notification was not found.");
}
