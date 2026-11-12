namespace Choco.Contract.Roles;

/// <summary>
/// A role and the permissions it holds, as an administrator sees it.
/// </summary>
/// <param name="Id">The role's id. Use it to assign or revoke the role.</param>
/// <param name="SystemKey">The stable key of a role the system created, such as member or admin; null for a role created by an administrator.</param>
/// <param name="Name">The role's display name.</param>
/// <param name="IsDefault">Whether every signed-in user holds this role without being assigned it.</param>
/// <param name="Permissions">The permission codes the role grants, such as roles.assign, in alphabetical order.</param>
/// <example>{"id":"00000000-0000-0000-0000-000000000001","systemKey":"admin","name":"Administrator","isDefault":false,"permissions":["audit-log.read","roles.assign"]}</example>
public sealed record RoleResponse(
    Guid Id,
    string? SystemKey,
    string Name,
    bool IsDefault,
    IReadOnlyList<string> Permissions);
