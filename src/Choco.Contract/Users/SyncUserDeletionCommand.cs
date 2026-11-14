using Choco.Contract.Abstractions.Messaging;

namespace Choco.Contract.Users;

public sealed record SyncUserDeletionCommand(string IdentityId) : ICommand, ISystemRequest;
