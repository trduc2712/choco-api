namespace Choco.Application.Abstractions.Authentication;

public sealed record IdpUser(string IdentityId, string? Username, string? FullName, string? AvatarUrl)
{
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public DateTimeOffset? UpdatedAt { get; init; }
}
