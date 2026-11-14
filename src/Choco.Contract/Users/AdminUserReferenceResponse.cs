namespace Choco.Contract.Users;

/// <summary>
/// A user named in the detail of another user, such as the administrator who suspended them.
/// </summary>
/// <param name="Id">The user's id.</param>
/// <param name="Username">The user's handle; it stays known after the user deletes their account.</param>
/// <example>{"id":"00000000-0000-0000-0000-000000000001","username":"username"}</example>
public sealed record AdminUserReferenceResponse(Guid Id, string Username);
