namespace Choco.Contract.Notifications;

/// <summary>How many notifications the signed-in user has not read.</summary>
/// <param name="Count">The number of unread notifications that are listed for the user.</param>
/// <example>{"count":3}</example>
public sealed record UnreadNotificationCountResponse(int Count);
