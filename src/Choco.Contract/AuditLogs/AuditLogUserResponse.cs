namespace Choco.Contract.AuditLogs;

/// <summary>
/// A user named in an audit log entry.
/// </summary>
/// <param name="Id">The user's id.</param>
/// <param name="Username">The user's username, or null when the account no longer exists.</param>
/// <example>{"id":"00000000-0000-0000-0000-000000000001","username":"username"}</example>
public sealed record AuditLogUserResponse(Guid Id, string? Username);
