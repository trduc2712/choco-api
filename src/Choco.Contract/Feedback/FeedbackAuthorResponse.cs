namespace Choco.Contract.Feedback;

/// <summary>The user who sent a piece of feedback.</summary>
/// <param name="Id">The user's id.</param>
/// <param name="Username">The username, up to 64 characters.</param>
/// <param name="DisplayName">The display name, up to 256 characters.</param>
/// <example>{"id":"00000000-0000-0000-0000-000000000001","username":"username","displayName":"Display Name"}</example>
public sealed record FeedbackAuthorResponse(Guid Id, string Username, string DisplayName);
