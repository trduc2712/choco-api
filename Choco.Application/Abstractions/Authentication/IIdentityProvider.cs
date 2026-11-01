namespace Choco.Application.Abstractions.Authentication;

public interface IIdentityProvider
{
    Task<IdpUser?> GetUserAsync(string identityId, CancellationToken cancellationToken = default);
}
