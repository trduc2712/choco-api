using Choco.Contract.Abstractions.Messaging;

namespace Choco.Contract.Users;

/// <summary>Replaces the caller's username and the name shown on their profile.</summary>
/// <param name="Username">The new username, up to 64 characters.</param>
/// <param name="FirstName">The first part of the display name. The full display name is limited to 256 characters.</param>
/// <param name="LastName">The last part of the display name. Omit it or send null to clear it.</param>
/// <example>{"username":"username","firstName":"First Name","lastName":"Last Name"}</example>
public sealed record UpdateMyProfileCommand(string Username, string FirstName, string? LastName = null) : ICommand<UserProfileResponse>;
