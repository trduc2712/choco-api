using Asp.Versioning;

using Choco.Contract.Users;
using Choco.Presentation.Abstractions;
using Choco.SharedKernel.Results;

using MediatR;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Choco.Presentation.Controllers;

/// <summary>
/// Users of choco-api and the roles they hold.
/// </summary>
[Route("api/v{version:apiVersion}/users")]
[ApiVersion(1.0)]
public sealed class UsersController : ApiController
{
    public UsersController(ISender sender) : base(sender) { }

    /// <summary>Gets a user's basic profile by username.</summary>
    /// <remarks>Any signed-in user with an active account may view this profile. The username match is exact and case-sensitive. Deleted users are not found. A first request may create the caller's local account from their sign-in provider details. Session and role information is not returned.</remarks>
    /// <param name="username" example="username">The exact username, required and up to 64 characters.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <response code="200">The user's profile.</response>
    /// <response code="400">The username or the caller's account details are invalid; errors lists the field.</response>
    /// <response code="401">The session is invalid or the caller's account is deleted.</response>
    /// <response code="404">The username does not exist or the user is deleted.</response>
    [HttpGet("by-username/{username}")]
    [ProducesResponseType(typeof(UserProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByUsername(string username, CancellationToken cancellationToken)
    {
        Result<UserProfileResponse> result = await Sender.Send(new GetUserByUsernameQuery(username), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : HandleFailure(result);
    }

    /// <summary>Gets a user's basic profile.</summary>
    /// <remarks>Any signed-in user with an active account may view this profile. Deleted users are not found. Session and role information is not returned.</remarks>
    /// <param name="userId" example="00000000-0000-0000-0000-000000000001">The user's id.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <response code="200">The user's profile.</response>
    /// <response code="400">The id is empty, or the caller's account details needed to create the local profile are invalid; errors lists the field.</response>
    /// <response code="401">The session is invalid or the caller's account is deleted.</response>
    /// <response code="404">The user does not exist or is deleted.</response>
    [HttpGet("{userId:guid}")]
    [ProducesResponseType(typeof(UserProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProfile(Guid userId, CancellationToken cancellationToken)
    {
        Result<UserProfileResponse> result = await Sender.Send(new GetUserProfileQuery(userId), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : HandleFailure(result);
    }

    /// <summary>
    /// Assigns a role to a user.
    /// </summary>
    /// <remarks>
    /// The caller needs the permission to assign roles, and must hold every permission the role grants.
    /// Assigning a role the user already holds changes nothing, so the call is safe to repeat.
    /// The default role cannot be assigned, because every signed-in user already has it.
    /// </remarks>
    /// <param name="userId" example="00000000-0000-0000-0000-000000000001">The id of the user who gets the role.</param>
    /// <param name="roleId" example="00000000-0000-0000-0000-000000000002">The id of the role to assign.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <response code="204">The user holds the role.</response>
    /// <response code="400">An id is empty, or the role is the default role.</response>
    /// <response code="401">The request has no valid session token, or the caller's account is deleted.</response>
    /// <response code="403">The caller may not assign roles, or lacks a permission the role grants.</response>
    /// <response code="404">No user or no role has this id.</response>
    [HttpPut("{userId:guid}/roles/{roleId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AssignRole(Guid userId, Guid roleId, CancellationToken cancellationToken)
    {
        Result result = await Sender.Send(new AssignRoleCommand(userId, roleId), cancellationToken);

        return result.IsSuccess ? NoContent() : HandleFailure(result);
    }

    /// <summary>
    /// Revokes a role from a user.
    /// </summary>
    /// <remarks>
    /// The caller needs the permission to assign roles, and must hold every permission the role grants.
    /// Revoking a role the user does not hold changes nothing, so the call is safe to repeat.
    /// The default role cannot be revoked, and the last administrator cannot lose the administrator role.
    /// </remarks>
    /// <param name="userId" example="00000000-0000-0000-0000-000000000001">The id of the user who loses the role.</param>
    /// <param name="roleId" example="00000000-0000-0000-0000-000000000002">The id of the role to revoke.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <response code="204">The user does not hold the role.</response>
    /// <response code="400">An id is empty, or the role is the default role.</response>
    /// <response code="401">The request has no valid session token, or the caller's account is deleted.</response>
    /// <response code="403">The caller may not assign roles, or lacks a permission the role grants.</response>
    /// <response code="404">No user or no role has this id.</response>
    /// <response code="409">The user is the last administrator and the role is the administrator role.</response>
    [HttpDelete("{userId:guid}/roles/{roleId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RevokeRole(Guid userId, Guid roleId, CancellationToken cancellationToken)
    {
        Result result = await Sender.Send(new RevokeRoleCommand(userId, roleId), cancellationToken);

        return result.IsSuccess ? NoContent() : HandleFailure(result);
    }
}
