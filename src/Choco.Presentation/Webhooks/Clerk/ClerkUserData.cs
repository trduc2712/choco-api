using Choco.Contract.Users;

namespace Choco.Presentation.Webhooks.Clerk;

internal sealed record ClerkUserData(
    string Id,
    string? Username,
    string? FirstName,
    string? LastName,
    string? ImageUrl,
    long? UpdatedAt)
{
    public CreateUserCommand ToCreateUserCommand()
    {
        return new CreateUserCommand(Id, Username ?? string.Empty, FullName(), ImageUrl, IdentityUpdatedAt());
    }

    public SyncUserProfileCommand ToSyncUserProfileCommand()
    {
        return new SyncUserProfileCommand(
            Id, Username ?? string.Empty, FullName(), ImageUrl, IdentityUpdatedAt() ?? DateTimeOffset.UnixEpoch);
    }

    private string? FullName()
    {
        string fullName = string.Join(' ', new[] { FirstName, LastName }.Where(name => !string.IsNullOrWhiteSpace(name)));

        return fullName.Length > 0 ? fullName : null;
    }

    private DateTimeOffset? IdentityUpdatedAt()
    {
        return UpdatedAt is { } updatedAt ? DateTimeOffset.FromUnixTimeMilliseconds(updatedAt) : null;
    }
}
