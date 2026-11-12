using Choco.Contract.Abstractions;
using Choco.Contract.Abstractions.Messaging;

namespace Choco.Contract.Users;

/// <summary>Lists users for administrators, newest sign-up first.</summary>
/// <param name="State">Only users in this state: active (not suspended), suspended or deleted. Null for every user who is not deleted, suspended or not.</param>
/// <param name="RoleId">Only users who hold this role. Holders of the default role are every user. Null for any role.</param>
/// <param name="Page">The page number, from 1. Defaults to 1 at the endpoint.</param>
/// <param name="PageSize">The page size, from 1 to 50. Defaults to 20 at the endpoint.</param>
/// <param name="Q">Part of the username or display name, taken as one phrase. Null or blank for no filter.</param>
/// <example>{"state":"active","roleId":"00000000-0000-0000-0000-000000000001","page":1,"pageSize":20,"q":"anh"}</example>
public sealed record GetAdminUsersQuery(string? State, Guid? RoleId, int Page, int PageSize, string? Q = null)
    : IQuery<OffsetPageResponse<AdminUserResponse>>
{
    public const int DefaultPage = 1;
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 50;
    public const int SearchMaxLength = 100;
}
