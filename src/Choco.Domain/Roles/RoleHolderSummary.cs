namespace Choco.Domain.Roles;

public sealed record RoleHolderSummary(Guid RoleId, int Count, IReadOnlyList<RoleHolder> TopHolders);

public sealed record RoleHolder(Guid Id, string Username, string DisplayName, string? AvatarUrl);
