using Choco.Contract.Abstractions.Messaging;

namespace Choco.Contract.Users;

public sealed record SyncUserProfileCommand(
    string IdentityId,
    string Username,
    string? DisplayName,
    string? AvatarUrl,
    DateTimeOffset IdentityUpdatedAt) : ICommand, ISystemRequest;
