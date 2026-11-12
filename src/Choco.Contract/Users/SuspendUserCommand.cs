using Choco.Contract.Abstractions.Messaging;

namespace Choco.Contract.Users;

/// <summary>
/// The reason to suspend a user.
/// </summary>
/// <param name="UserId">Ignored; the user is the one named in the route.</param>
/// <param name="Reason">Why the user is suspended. Required, at most 150 characters once leading and trailing whitespace is removed. The suspended user sees it.</param>
/// <example>{"reason":"Repeated spam"}</example>
public sealed record SuspendUserCommand(Guid UserId, string Reason) : ICommand;
