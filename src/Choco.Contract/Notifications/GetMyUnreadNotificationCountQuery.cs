using Choco.Contract.Abstractions.Messaging;

namespace Choco.Contract.Notifications;

public sealed record GetMyUnreadNotificationCountQuery : IQuery<UnreadNotificationCountResponse>;
