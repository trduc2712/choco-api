using Choco.Contract.Abstractions.Messaging;

namespace Choco.Contract.Users;

/// <summary>Reads one user for administrators.</summary>
/// <param name="UserId">The id of the user to read.</param>
/// <example>{"userId":"00000000-0000-0000-0000-000000000001"}</example>
public sealed record GetAdminUserQuery(Guid UserId) : IQuery<AdminUserDetailResponse>;
