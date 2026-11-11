using Choco.Domain.Users;

namespace Choco.Domain.UserBlocks;

public sealed record UserBlockEntry(Guid BlockId, DateTimeOffset BlockedAt, User User);
