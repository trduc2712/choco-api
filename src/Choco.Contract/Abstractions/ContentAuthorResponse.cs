namespace Choco.Contract.Abstractions;

/// <summary>
/// The author of a post or comment, as an administrator sees them.
/// </summary>
/// <param name="Id">The author's id.</param>
/// <param name="Username">The author's handle; it stays known after the author deletes their account.</param>
/// <example>{"id":"00000000-0000-0000-0000-000000000001","username":"username"}</example>
public sealed record ContentAuthorResponse(Guid Id, string? Username);
