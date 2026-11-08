namespace Choco.Domain.Users;

public sealed record UserSearchMatch(Guid Id, string Username, string DisplayName, string? AvatarUrl, int Rank);
