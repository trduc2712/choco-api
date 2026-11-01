namespace Choco.Application.Abstractions.Authentication;

public sealed record IdpUser(string IdentityId, string? Username, string? FullName, string? AvatarUrl);
