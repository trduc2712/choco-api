namespace Choco.Contract.Roles;

/// <summary>
/// A role and the permissions it holds, as an administrator sees it.
/// </summary>
/// <param name="Id">The role's id. Use it to assign or revoke the role.</param>
/// <param name="SystemKey">The stable key of a role the system created, such as member or admin; null for a role created by an administrator.</param>
/// <param name="Name">The role's display name.</param>
/// <param name="IsDefault">Whether every signed-in user holds this role without being assigned it.</param>
/// <param name="Permissions">The permission codes the role grants, such as roles.assign, in alphabetical order.</param>
/// <param name="Description">The role's purpose, or null.</param>
/// <param name="Color">The role's display color key.</param>
/// <param name="Icon">The role's display icon key.</param>
/// <param name="HolderCount">The number of users who hold the role, excluding deleted users. For the default role, this includes all users who are not deleted.</param>
/// <param name="TopHolders">Up to three holders, newest assignment first. Empty for the default role.</param>
/// <param name="ModifiedAt">The time the role was last changed, in ISO 8601 format.</param>
/// <param name="ModifiedBy">The last user to create or update the role, or null if the user is unavailable or no user was recorded.</param>
/// <example>{"id":"00000000-0000-0000-0000-000000000001","systemKey":"admin","name":"Administrator","isDefault":false,"permissions":["audit-log.read","roles.assign"],"description":"Manages roles, users, and community operations.","color":"purple","icon":"crown","holderCount":1,"topHolders":[{"id":"00000000-0000-0000-0000-000000000002","username":"username","displayName":"Display Name","avatarUrl":"https://example.com/avatar.png"}],"modifiedAt":"2026-01-01T00:00:00+00:00","modifiedBy":{"id":"00000000-0000-0000-0000-000000000003","username":"username-2"}}</example>
public sealed record RoleResponse(
    Guid Id,
    string? SystemKey,
    string Name,
    bool IsDefault,
    IReadOnlyList<string> Permissions,
    string? Description,
    string Color,
    string Icon,
    int HolderCount,
    IReadOnlyList<RoleHolderResponse> TopHolders,
    DateTimeOffset ModifiedAt,
    RoleModifiedByResponse? ModifiedBy);
