using Choco.Application.Abstractions.Authentication;
using Choco.Application.Abstractions.Messaging;
using Choco.Contract.Notifications;
using Choco.Domain.Notifications;
using Choco.SharedKernel.Results;

namespace Choco.Application.Notifications.GetMyUnreadNotificationCount;

internal sealed class GetMyUnreadNotificationCountQueryHandler(
    ICurrentUser currentUser,
    INotificationRepository notificationRepository)
    : IQueryHandler<GetMyUnreadNotificationCountQuery, UnreadNotificationCountResponse>
{
    public async Task<Result<UnreadNotificationCountResponse>> Handle(
        GetMyUnreadNotificationCountQuery query, CancellationToken cancellationToken)
    {
        Result<Guid> caller = await currentUser.GetIdAsync(cancellationToken);
        if (caller.IsFailure)
        {
            return Result<UnreadNotificationCountResponse>.Failure(caller.Error);
        }

        int count = await notificationRepository.CountUnreadForReadAsync(caller.Value, cancellationToken);

        return Result<UnreadNotificationCountResponse>.Success(new UnreadNotificationCountResponse(count));
    }
}
