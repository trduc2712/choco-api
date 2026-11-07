using Choco.Contract.Abstractions.Messaging;

namespace Choco.Contract.Users;

/// <summary>Finds a user's basic profile by their exact username.</summary>
/// <param name="Username">The exact, case-sensitive username, required and up to 64 characters.</param>
/// <example>{"username":"username"}</example>
public sealed record GetUserByUsernameQuery(string Username) : IQuery<UserProfileResponse>;
