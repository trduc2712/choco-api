namespace Choco.Contract.Roles;

/// <summary>The effect of replacing a role's permissions.</summary>
/// <param name="AffectedHolderCount">The number of non-deleted holders, or zero when the permission set is unchanged.</param>
/// <param name="AddedPermissions">The permission codes added by the proposed change, in alphabetical order.</param>
/// <param name="RemovedPermissions">The permission codes removed by the proposed change, in alphabetical order, with their retention counts.</param>
/// <example>{"affectedHolderCount":12,"addedPermissions":["reports.handle"],"removedPermissions":[{"code":"posts.delete-any","keptByHolderCount":3}]}</example>
public sealed record RoleImpactResponse(
    int AffectedHolderCount,
    IReadOnlyList<string> AddedPermissions,
    IReadOnlyList<RemovedRolePermissionResponse> RemovedPermissions);

/// <summary>A permission the proposed change removes.</summary>
/// <param name="Code">The removed permission code.</param>
/// <param name="KeptByHolderCount">The number of holders who keep this permission through another role.</param>
/// <example>{"code":"posts.delete-any","keptByHolderCount":3}</example>
public sealed record RemovedRolePermissionResponse(string Code, int KeptByHolderCount);
