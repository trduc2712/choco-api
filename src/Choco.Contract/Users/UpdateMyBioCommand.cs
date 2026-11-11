using Choco.Contract.Abstractions.Messaging;

namespace Choco.Contract.Users;

/// <summary>Replaces the caller's bio.</summary>
/// <param name="Bio">The new bio, up to 160 characters after trimming. Omit it, send null or send blank text to clear it.</param>
/// <example>{"bio":"Coffee, books and long walks."}</example>
public sealed record UpdateMyBioCommand(string? Bio = null) : ICommand<UserProfileResponse>;
