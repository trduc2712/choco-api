using Choco.Contract.Notifications;

using FluentValidation;

namespace Choco.Application.Notifications.GetMyNotifications;

internal sealed class GetMyNotificationsQueryValidator : AbstractValidator<GetMyNotificationsQuery>
{
    public GetMyNotificationsQueryValidator()
    {
        RuleFor(query => query.Limit).InclusiveBetween(1, GetMyNotificationsQuery.MaxLimit);

        RuleFor(query => query.Cursor)
            .Must(cursor => NotificationCursor.TryDecode(cursor, out _))
            .When(query => query.Cursor is not null)
            .WithMessage("'{PropertyName}' is not a valid cursor.");
    }
}
