namespace Choco.Contract.Users;

/// <summary>
/// A role assigned to a user.
/// </summary>
/// <param name="Id">The role's id.</param>
/// <param name="Name">The role's name.</param>
/// <example>{"id":"00000000-0000-0000-0000-000000000002","name":"Administrator"}</example>
public sealed record AdminUserRoleResponse(Guid Id, string Name);
