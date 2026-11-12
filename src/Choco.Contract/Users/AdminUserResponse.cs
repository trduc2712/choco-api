namespace Choco.Contract.Users;

/// <summary>
/// A user as an administrator sees them.
/// </summary>
/// <param name="Id">The user's id.</param>
/// <param name="Username">The user's handle.</param>
/// <param name="DisplayName">The name shown to other people.</param>
/// <param name="CreatedAt">When the user signed up, in UTC.</param>
/// <param name="Roles">The roles assigned to the user, ordered by name. The default role belongs to every user and is not listed.</param>
/// <param name="IsSuspended">Whether an administrator suspended the user and has not lifted the suspension.</param>
/// <param name="IsDeleted">Whether the user deleted their account.</param>
/// <example>{"id":"00000000-0000-0000-0000-000000000001","username":"username","displayName":"Display Name","createdAt":"2026-01-01T00:00:00+00:00","roles":[{"id":"00000000-0000-0000-0000-000000000002","name":"Administrator"}],"isSuspended":false,"isDeleted":false}</example>
public sealed record AdminUserResponse(
    Guid Id,
    string Username,
    string DisplayName,
    DateTimeOffset CreatedAt,
    IReadOnlyList<AdminUserRoleResponse> Roles,
    bool IsSuspended,
    bool IsDeleted);
