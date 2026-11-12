using Choco.Contract.Abstractions.Messaging;

namespace Choco.Contract.Roles;

/// <summary>Gets one role with the permissions it holds.</summary>
/// <param name="Id">The role's id.</param>
/// <example>{"id":"00000000-0000-0000-0000-000000000001"}</example>
public sealed record GetRoleByIdQuery(Guid Id) : IQuery<RoleResponse>;
