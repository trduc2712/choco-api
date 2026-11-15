using Choco.Contract.Abstractions.Messaging;

namespace Choco.Contract.Roles;

/// <summary>
/// The new name, appearance and permissions of a role.
/// </summary>
/// <param name="Id">Ignored; the role is the one named in the route.</param>
/// <param name="Name">The role's name. Required, at most 64 characters once leading and trailing whitespace is removed. It must differ from every other role's name, ignoring case.</param>
/// <param name="Permissions">The permission codes the role grants from now on, replacing the current ones. Codes come from the list at GET /api/v1/admin/permissions. May be empty. A code listed twice counts once. The caller must hold every permission listed.</param>
/// <param name="Description">Optional purpose of the role, at most 200 characters after trimming. A blank value is stored as null.</param>
/// <param name="Color">Required color key: purple, coral, teal, blue, pink, amber or gray.</param>
/// <param name="Icon">Required icon key: user, shield, crown, star, flag, eye, key or wrench.</param>
/// <example>{"name":"Role Name","permissions":["reports.handle"],"description":"Role Description","color":"teal","icon":"shield"}</example>
public sealed record UpdateRoleCommand(
    Guid Id,
    string Name,
    IReadOnlyList<string> Permissions,
    string? Description,
    string Color,
    string Icon) : ICommand<RoleResponse>;
