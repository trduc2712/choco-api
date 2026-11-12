using Choco.Contract.Abstractions.Messaging;

namespace Choco.Contract.Roles;

/// <summary>
/// A role to create.
/// </summary>
/// <param name="Name">The role's name. Required, at most 64 characters once leading and trailing whitespace is removed. It must differ from every other role's name, ignoring case.</param>
/// <param name="Permissions">The permission codes the role grants, from the list at GET /api/v1/admin/permissions. May be empty. A code listed twice counts once. The caller must hold every permission listed.</param>
/// <example>{"name":"Moderator","permissions":["reports.handle","posts.delete-any"]}</example>
public sealed record CreateRoleCommand(string Name, IReadOnlyList<string> Permissions) : ICommand<RoleResponse>;
