using Choco.Contract.Abstractions.Messaging;

namespace Choco.Contract.Roles;

/// <summary>Lists every role with the permissions it holds.</summary>
public sealed record GetRolesQuery : IQuery<IReadOnlyList<RoleResponse>>;
