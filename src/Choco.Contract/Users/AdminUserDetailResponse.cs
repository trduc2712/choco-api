namespace Choco.Contract.Users;

/// <summary>
/// One user as an administrator sees them: the whole picture for a decision about a report or a dispute.
/// </summary>
/// <param name="Id">The user's id.</param>
/// <param name="Username">The user's handle.</param>
/// <param name="DisplayName">The name shown to other people.</param>
/// <param name="Bio">The user's bio, or null when they have none.</param>
/// <param name="AvatarUrl">The address of the user's avatar, or null when they have none.</param>
/// <param name="CreatedAt">When the user signed up, in UTC.</param>
/// <param name="Roles">The roles assigned to the user, ordered by name. The default role belongs to every user and is not listed.</param>
/// <param name="IsSuspended">Whether an administrator suspended the user and has not lifted the suspension.</param>
/// <param name="Suspension">The reason, the time and the user who suspended them; null when the user is not suspended.</param>
/// <param name="IsDeleted">Whether the user deleted their account.</param>
/// <param name="DeletedAt">When the user deleted their account, in UTC; null when they did not.</param>
/// <param name="OpenReportCount">How many open reports are about the user themselves. Reports about their posts are not counted.</param>
/// <example>{"id":"00000000-0000-0000-0000-000000000001","username":"username","displayName":"Display Name","bio":"bio","avatarUrl":"https://example.com/avatar.png","createdAt":"2026-01-01T00:00:00+00:00","roles":[{"id":"00000000-0000-0000-0000-000000000003","name":"Moderator"}],"isSuspended":true,"suspension":{"reason":"Reported for spam","suspendedAt":"2026-01-02T00:00:00+00:00","suspendedBy":{"id":"00000000-0000-0000-0000-000000000002","username":"username-2"}},"isDeleted":false,"deletedAt":null,"openReportCount":2}</example>
public sealed record AdminUserDetailResponse(
    Guid Id,
    string Username,
    string DisplayName,
    string? Bio,
    string? AvatarUrl,
    DateTimeOffset CreatedAt,
    IReadOnlyList<AdminUserRoleResponse> Roles,
    bool IsSuspended,
    AdminUserSuspensionResponse? Suspension,
    bool IsDeleted,
    DateTimeOffset? DeletedAt,
    int OpenReportCount);
