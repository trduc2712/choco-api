using Choco.Contract.Abstractions;
using Choco.Contract.Notifications;
using Choco.Domain.Notifications;

namespace Choco.Application.Notifications;

internal static class NotificationPages
{
    public static (DateTimeOffset CreatedAt, Guid NotificationId)? ReadPosition(string? cursor)
    {
        return NotificationCursor.TryDecode(cursor, out NotificationCursor? position)
            ? (position!.CreatedAt, position.NotificationId)
            : null;
    }

    public static NotificationResponse ToResponse(NotificationEntry entry)
    {
        return new NotificationResponse(
            entry.Notification.Id,
            entry.Notification.Type.ToString(),
            new NotificationActorResponse(
                entry.Actor.Id, entry.Actor.Username, entry.Actor.DisplayName, entry.Actor.AvatarUrl),
            entry.Notification.PostId,
            NotificationExcerpt.Of(entry.Post?.Content),
            entry.Notification.CommentId,
            NotificationExcerpt.Of(entry.Comment?.Content),
            entry.Notification.CreatedAt,
            entry.Notification.ReadAt is not null);
    }

    public static PageResponse<NotificationResponse> ToPage(IReadOnlyList<NotificationEntry> entries, int limit)
    {
        List<NotificationEntry> page = entries.Take(limit).ToList();
        string? nextCursor = entries.Count > limit
            ? new NotificationCursor(page[^1].Notification.CreatedAt, page[^1].Notification.Id).Encode()
            : null;

        return new PageResponse<NotificationResponse>(page.Select(ToResponse).ToList(), nextCursor);
    }
}
