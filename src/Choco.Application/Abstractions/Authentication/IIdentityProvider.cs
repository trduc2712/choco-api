using Choco.SharedKernel.Results;

namespace Choco.Application.Abstractions.Authentication;

public interface IIdentityProvider
{
    Task<Result<IdpUser>> UpdateProfileAsync(
        string identityId, string username, string firstName, string lastName, CancellationToken cancellationToken);
    Task<Result<IdpUser>> UploadAvatarAsync(string identityId, Stream image, CancellationToken cancellationToken);
    Task<Result> RemoveAvatarAsync(string identityId, CancellationToken cancellationToken);
    Task<IdpUser?> GetUserAsync(string identityId, CancellationToken cancellationToken = default);
}
