using Choco.Contract.Abstractions.Messaging;

namespace Choco.Contract.Users;

/// <summary>Follows a user.</summary>
/// <param name="UserId">The target user's id.</param>
/// <example>{"userId":"00000000-0000-0000-0000-000000000001"}</example>
public sealed record FollowUserCommand(Guid UserId) : ICommand;
