using Choco.Contract.Users;

namespace Choco.Presentation.Webhooks.Clerk;

internal sealed record ClerkUserData(
    string Id,
    string? Username,
    string? FirstName,
    string? LastName,
    string? ImageUrl)
{
    public CreateUserCommand ToCreateUserCommand()
    {
        string fullName = string.Join(' ', new[] { FirstName, LastName }.Where(name => !string.IsNullOrWhiteSpace(name)));

        return new CreateUserCommand(Id, Username ?? string.Empty, fullName.Length > 0 ? fullName : null, ImageUrl);
    }
}
