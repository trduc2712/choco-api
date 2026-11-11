using Choco.Domain.Comments;
using Choco.Domain.Posts;
using Choco.Domain.Users;

namespace Choco.Domain.Notifications;

public sealed record NotificationEntry(Notification Notification, User Actor, Post? Post = null, Comment? Comment = null);
