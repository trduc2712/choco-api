using Asp.Versioning;

using Choco.Contract.Abstractions;
using Choco.Contract.Users;
using Choco.Presentation.Abstractions;
using Choco.SharedKernel.Results;

using MediatR;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Choco.Presentation.Controllers;

/// <summary>
/// Users as administrators see them: roles and deleted accounts included.
/// </summary>
[Route("api/v{version:apiVersion}/admin/users")]
[ApiVersion(1.0)]
public sealed class AdminUsersController : ApiController
{
    public AdminUsersController(ISender sender) : base(sender) { }

    /// <summary>Lists users, newest sign-up first.</summary>
    /// <remarks>Only a caller with the permission to read any user may call it. Use state, roleId and q to narrow the list; leave them out for all active users. All filters combine with AND. Deleted users are left out unless state is deleted, so an account that an audit log entry points to can still be found. A search ignores case and accents (a, á and ả match each other; d and đ match each other) and surrounding whitespace, and a blank q is ignored. Filtering by the default role matches every user. Each entry lists the roles assigned to the user; the default role is not listed. Pages are numbered from 1. The response carries the page, the page size and totalCount, the number of users that match the filters across all pages. A page past the last one is not an error; its items are empty.</remarks>
    /// <param name="state" example="active">Only users in this state: active or deleted. Defaults to active.</param>
    /// <param name="roleId" example="00000000-0000-0000-0000-000000000001">Only users who hold this role. An id that matches no role gives an empty list.</param>
    /// <param name="q" example="anh">Part of the username or display name, taken as one phrase. At most 100 characters.</param>
    /// <param name="page" example="1">The page number, from 1. Defaults to 1.</param>
    /// <param name="pageSize" example="20">The page size, from 1 to 50. Defaults to 20.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <response code="200">One page of users and the total count. The page can be empty.</response>
    /// <response code="400">The state, role id, page or page size is invalid, or q is longer than 100 characters.</response>
    /// <response code="401">The session is invalid or the caller's account is deleted.</response>
    /// <response code="403">The caller may not read users.</response>
    [HttpGet]
    [ProducesResponseType(typeof(OffsetPageResponse<AdminUserResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAll(
        string? state = null,
        Guid? roleId = null,
        string? q = null,
        int page = GetAdminUsersQuery.DefaultPage,
        int pageSize = GetAdminUsersQuery.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        Result<OffsetPageResponse<AdminUserResponse>> result = await Sender.Send(
            new GetAdminUsersQuery(state, roleId, page, pageSize, q), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : HandleFailure(result);
    }
}
