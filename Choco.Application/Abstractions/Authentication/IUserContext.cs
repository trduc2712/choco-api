namespace Choco.Application.Abstractions.Authentication;

/// <summary>
/// The identity of the caller as the identity provider sees it, read from the access token.
/// Use <see cref="ICurrentUser"/> to get the caller's id in this system (<c>users.id</c>).
/// </summary>
public interface IUserContext
{
    string IdentityId { get; }
}
