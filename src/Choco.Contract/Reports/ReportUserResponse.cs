namespace Choco.Contract.Reports;

/// <summary>A user named in a report: the reporter or the user who resolved it.</summary>
/// <param name="Id">The user's id.</param>
/// <param name="Username">The user's username, or null when that account is deleted.</param>
/// <example>{"id":"00000000-0000-0000-0000-000000000001","username":"username"}</example>
public sealed record ReportUserResponse(Guid Id, string? Username);
