using Choco.Contract.Abstractions.Messaging;

namespace Choco.Contract.Roles;

/// <summary>Previews the effect of replacing a role's permissions.</summary>
/// <param name="Id">The role's id.</param>
/// <param name="Permissions">The proposed permission codes. An empty list removes all permissions.</param>
/// <example>{"id":"00000000-0000-0000-0000-000000000001","permissions":["reports.handle"]}</example>
public sealed record GetRoleImpactQuery(Guid Id, IReadOnlyList<string> Permissions) : IQuery<RoleImpactResponse>;
