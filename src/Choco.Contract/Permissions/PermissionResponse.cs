namespace Choco.Contract.Permissions;

/// <summary>
/// A permission a role can hold.
/// </summary>
/// <param name="Code">The permission's code, such as roles.assign. It is the value a role lists in its permissions.</param>
/// <example>{"code":"roles.assign"}</example>
public sealed record PermissionResponse(string Code);
