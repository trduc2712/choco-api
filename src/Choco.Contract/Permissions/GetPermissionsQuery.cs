using Choco.Contract.Abstractions.Messaging;

namespace Choco.Contract.Permissions;

/// <summary>Lists every permission a role can hold.</summary>
public sealed record GetPermissionsQuery : IQuery<IReadOnlyList<PermissionResponse>>;
