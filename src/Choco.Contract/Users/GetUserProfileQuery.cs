using Choco.Contract.Abstractions.Messaging;

namespace Choco.Contract.Users;

public sealed record GetUserProfileQuery(Guid Id) : IQuery<UserProfileResponse>;
