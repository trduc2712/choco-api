namespace Choco.Contract.Users;

/// <summary>
/// The signed-in caller as the API knows them.
/// </summary>
/// <param name="Id">The user's id.</param>
/// <param name="IdentityId">The caller's id at the identity provider, from the session token.</param>
/// <param name="SessionId">The id of the caller's session, from the session token.</param>
/// <param name="AuthorizedParty">The client the session token was issued to, from the session token.</param>
/// <param name="Permissions">The permission codes of the default role and of every role assigned to the caller, without duplicates, sorted alphabetically. Empty when the caller holds none. Clients use it to decide what to show; the API still checks every request.</param>
/// <example>{"id":"00000000-0000-0000-0000-000000000001","identityId":"user_2abcDEF123","sessionId":"sess_2abcDEF123","authorizedParty":"https://choco.example.com","permissions":["feedback.read-any","reports.handle"]}</example>
public sealed record MeResponse(
    Guid Id,
    string? IdentityId,
    string? SessionId,
    string? AuthorizedParty,
    IReadOnlyList<string> Permissions);
