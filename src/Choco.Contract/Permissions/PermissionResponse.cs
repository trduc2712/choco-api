namespace Choco.Contract.Permissions;

/// <summary>
/// A permission a role can hold.
/// </summary>
/// <param name="Code">The permission's code, such as roles.assign. It is the value a role lists in its permissions.</param>
/// <param name="Group">The permission group: roles, content, reports, feedback, users or audit-log.</param>
/// <param name="IsSensitive">Whether the permission allows a sensitive action.</param>
/// <example>{"code":"roles.assign","group":"roles","isSensitive":true}</example>
public sealed record PermissionResponse(string Code, string Group, bool IsSensitive);
