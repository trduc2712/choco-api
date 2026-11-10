namespace Choco.Contract.Notifications;

/// <summary>A notification for the signed-in user.</summary>
/// <param name="Id">The notification's id, to use when marking it read.</param>
/// <param name="Type">What happened. Followed means the actor followed the user.</param>
/// <param name="Actor">The user whose action caused the notification.</param>
/// <param name="CreatedAt">When it happened, in UTC.</param>
/// <param name="IsRead">Whether the user has marked it read.</param>
/// <example>{"id":"00000000-0000-0000-0000-000000000001","type":"Followed","actor":{"id":"00000000-0000-0000-0000-000000000002","username":"username","displayName":"Display Name","avatarUrl":"https://example.com/avatar.png"},"createdAt":"2026-01-01T00:00:00+00:00","isRead":false}</example>
public sealed record NotificationResponse(
    Guid Id, string Type, NotificationActorResponse Actor, DateTimeOffset CreatedAt, bool IsRead);
