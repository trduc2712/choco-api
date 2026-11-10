using Choco.Contract.Abstractions;
using Choco.Contract.Abstractions.Messaging;

namespace Choco.Contract.Notifications;

/// <summary>Lists the caller's notifications, newest first.</summary>
/// <param name="Cursor">The previous page's nextCursor, or null for the first page.</param>
/// <param name="Limit">The page size, from 1 to 50. Defaults to 20 at the endpoint.</param>
/// <example>{ "cursor": null, "limit": 20 }</example>
public sealed record GetMyNotificationsQuery(string? Cursor, int Limit) : IQuery<PageResponse<NotificationResponse>>
{
    public const int DefaultLimit = 20;
    public const int MaxLimit = 50;
}
