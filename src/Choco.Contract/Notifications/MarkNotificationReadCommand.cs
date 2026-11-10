using Choco.Contract.Abstractions.Messaging;

namespace Choco.Contract.Notifications;

/// <summary>Marks one of the caller's notifications as read.</summary>
/// <param name="NotificationId">The notification's id.</param>
/// <example>{"notificationId":"00000000-0000-0000-0000-000000000001"}</example>
public sealed record MarkNotificationReadCommand(Guid NotificationId) : ICommand;
