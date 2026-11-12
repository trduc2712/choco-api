using Choco.SharedKernel.Results;

namespace Choco.Application.Abstractions.Authentication;

/// <summary>
/// The caller as a user of this system. Creates the user from the identity provider when the
/// <c>user.created</c> webhook has not arrived yet, which saves changes on the request's unit of work:
/// call <see cref="GetIdAsync"/> before adding entities in a handler.
/// </summary>
public interface ICurrentUser
{
    Task<Result<Guid>> GetIdAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// The caller's suspension, or null when the caller is not suspended. Read from the same user row
    /// as <see cref="GetIdAsync"/>, so it costs no extra query.
    /// </summary>
    Task<Result<CurrentUserSuspension?>> GetSuspensionAsync(CancellationToken cancellationToken = default);
}
