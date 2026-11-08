using Choco.Domain.Users;

namespace Choco.Domain.UserFollows;

public sealed record UserFollowEntry(Guid FollowId, DateTimeOffset FollowedAt, User User);
