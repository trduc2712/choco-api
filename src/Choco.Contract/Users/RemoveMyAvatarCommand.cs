using Choco.Contract.Abstractions.Messaging;

namespace Choco.Contract.Users;

public sealed record RemoveMyAvatarCommand : ICommand<UserProfileResponse>;
