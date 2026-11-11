using Choco.Contract.Abstractions.Messaging;

namespace Choco.Contract.Users;

public sealed record GetMyPermissionsQuery : IQuery<MyPermissionsResponse>;
