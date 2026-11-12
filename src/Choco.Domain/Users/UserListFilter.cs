namespace Choco.Domain.Users;

public sealed record UserListFilter(string? Text, Guid? RoleId, UserState? State);
