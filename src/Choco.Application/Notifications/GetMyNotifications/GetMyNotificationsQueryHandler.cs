using Choco.Application.Abstractions.Authentication;
using Choco.Application.Abstractions.Messaging;
using Choco.Contract.Abstractions;
using Choco.Contract.Notifications;
using Choco.Domain.Notifications;
using Choco.SharedKernel.Results;

namespace Choco.Application.Notifications.GetMyNotifications;

internal sealed class GetMyNotificationsQueryHandler(
    ICurrentUser currentUser,
    INotificationRepository notificationRepository)
    : IQueryHandler<GetMyNotificationsQuery, PageResponse<NotificationResponse>>
{
    public async Task<Result<PageResponse<NotificationResponse>>> Handle(
        GetMyNotificationsQuery query, CancellationToken cancellationToken)
    {
        Result<Guid> caller = await currentUser.GetIdAsync(cancellationToken);
        if (caller.IsFailure)
        {
            return Result<PageResponse<NotificationResponse>>.Failure(caller.Error);
        }

        IReadOnlyList<NotificationEntry> entries = await notificationRepository.ListForReadAsync(
            caller.Value, NotificationPages.ReadPosition(query.Cursor), query.Limit + 1, cancellationToken);

        return Result<PageResponse<NotificationResponse>>.Success(NotificationPages.ToPage(entries, query.Limit));
    }
}
