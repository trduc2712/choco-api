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

    /// <summary>Suspends a user.</summary>
    /// <remarks>Only a caller with the permission to suspend users may call it. A suspended user can still sign in and read, but every request that creates or changes data is refused until the suspension is lifted. The reason is required and the suspended user sees it. The caller must hold every permission the user holds, so an administrator can suspend another administrator but a user with fewer permissions cannot. Suspending an already suspended user with the same reason changes nothing; with another reason it replaces the reason and records the caller and the time again.</remarks>
    /// <param name="userId" example="00000000-0000-0000-0000-000000000001">The id of the user to suspend.</param>
    /// <param name="command">The reason for the suspension.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <response code="204">The user is suspended.</response>
    /// <response code="400">The user id is empty, the reason is missing, blank or too long, or the caller is suspending themselves.</response>
    /// <response code="401">The session is invalid or the caller's account is deleted.</response>
    /// <response code="403">The caller may not suspend users, or lacks a permission of the user.</response>
    /// <response code="404">No user has this id, or the user is deleted.</response>
    /// <response code="409">The user is the last administrator who is not suspended.</response>
    [HttpPut("{userId:guid}/suspension")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Suspend(Guid userId, [FromBody] SuspendUserCommand command, CancellationToken cancellationToken)
    {
        Result result = await Sender.Send(command with { UserId = userId }, cancellationToken);

        return result.IsSuccess ? NoContent() : HandleFailure(result);
    }

    /// <summary>Lifts the suspension of a user.</summary>
    /// <remarks>Only a caller with the permission to suspend users may call it. Lifting the suspension of a user who is not suspended changes nothing, so the call is safe to repeat. The user can create and change data again from their next request.</remarks>
    /// <param name="userId" example="00000000-0000-0000-0000-000000000001">The id of the user.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <response code="204">The user is not suspended.</response>
    /// <response code="400">The user id is empty.</response>
    /// <response code="401">The session is invalid or the caller's account is deleted.</response>
    /// <response code="403">The caller may not suspend users.</response>
    /// <response code="404">No user has this id, or the user is deleted.</response>
    [HttpDelete("{userId:guid}/suspension")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Unsuspend(Guid userId, CancellationToken cancellationToken)
    {
        Result result = await Sender.Send(new UnsuspendUserCommand(userId), cancellationToken);

        return result.IsSuccess ? NoContent() : HandleFailure(result);
    }

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
