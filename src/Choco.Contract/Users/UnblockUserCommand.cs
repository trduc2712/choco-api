using Choco.Contract.Abstractions.Messaging;

namespace Choco.Contract.Users;

/// <summary>Unblocks a user.</summary>
/// <param name="UserId">The target user's id.</param>
/// <example>{"userId":"00000000-0000-0000-0000-000000000001"}</example>
public sealed record UnblockUserCommand(Guid UserId) : ICommand;
