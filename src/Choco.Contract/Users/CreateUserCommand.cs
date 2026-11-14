using Choco.Contract.Abstractions.Messaging;

namespace Choco.Contract.Users;

public sealed record CreateUserCommand(
    string IdentityId,
    string Username,
    string? DisplayName,
    string? AvatarUrl,
    DateTimeOffset? IdentityUpdatedAt = null) : ICommand, ISystemRequest;
