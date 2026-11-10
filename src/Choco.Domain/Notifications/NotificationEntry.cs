using Choco.Domain.Users;

namespace Choco.Domain.Notifications;

public sealed record NotificationEntry(Notification Notification, User Actor);
