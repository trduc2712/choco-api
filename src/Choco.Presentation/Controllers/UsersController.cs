using Asp.Versioning;

using Choco.Contract.Abstractions;
using Choco.Contract.Posts;
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

    /// <summary>Suggests up to ten users as the caller types.</summary>
    /// <remarks>Any signed-in user with an active account may call this endpoint. Results match the username or display name and exclude the caller and deleted accounts. An empty search returns an empty list.</remarks>
    /// <param name="q" example="username">Search text, up to 256 characters after trimming. Matching starts at one character.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <response code="200">User suggestions in relevance order.</response>
    /// <response code="400">The search text or the caller's account details are invalid.</response>
    /// <response code="401">The session is invalid or the caller's account is deleted.</response>
    [HttpGet("suggestions")]
    [ProducesResponseType(typeof(IReadOnlyList<UserSearchItemResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetSuggestions([FromQuery] string? q, CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<UserSearchItemResponse>> result = await Sender.Send(
            new GetUserSuggestionsQuery(q), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : HandleFailure(result);
    }

    /// <summary>Searches for users by username or display name.</summary>
    /// <remarks>Any signed-in user with an active account may call this endpoint. Each page contains up to ten users, excluding the caller and deleted accounts. Use nextCursor with the same search text to get the next page.</remarks>
    /// <param name="q" example="username">Search text, up to 256 characters after trimming. Matching starts at one character.</param>
    /// <param name="cursor" example="MjowMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMToxNkY3OEE3RDYzMTdGMTAyQkJEOTVGQzlBNEYzRkYyRTMyNDkyODc2OTBCOEJEQUQ2Qjc4MTBGODJCMzRBQ0UzOnVzZXJuYW1lLTEw">The previous page's nextCursor. Omit it for the first page.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <response code="200">A page of users in relevance order. An empty search returns an empty page.</response>
    /// <response code="400">The search text, cursor or the caller's account details are invalid.</response>
    /// <response code="401">The session is invalid or the caller's account is deleted.</response>
    [HttpGet("search")]
    [ProducesResponseType(typeof(PageResponse<UserSearchItemResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetSearchResults(
        [FromQuery] string? q, [FromQuery] string? cursor, CancellationToken cancellationToken)
    {
        Result<PageResponse<UserSearchItemResponse>> result = await Sender.Send(
            new GetUserSearchResultsQuery(q, cursor), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : HandleFailure(result);
    }

    /// <summary>Gets a user's basic profile by username.</summary>
    /// <remarks>Any signed-in user with an active account may view this profile. The username match is exact and case-sensitive. Deleted users are not found. The profile shows whether the caller follows the user and how many users follow them and are followed by them; deleted accounts are not counted. A first request may create the caller's local account from their sign-in provider details. Session and role information is not returned.</remarks>
    /// <param name="username" example="username">The exact username, required and up to 64 characters.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <response code="200">The user's profile.</response>
    /// <response code="400">The username or the caller's account details are invalid; errors lists the field.</response>
    /// <response code="401">The session is invalid or the caller's account is deleted.</response>
    /// <response code="404">The username does not exist or the user is deleted.</response>
    [HttpGet("by-username/{username}")]
    [ProducesResponseType(typeof(UserProfileDetailsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByUsername(string username, CancellationToken cancellationToken)
    {
        Result<UserProfileDetailsResponse> result = await Sender.Send(new GetUserByUsernameQuery(username), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : HandleFailure(result);
    }

    /// <summary>Gets a user's basic profile.</summary>
    /// <remarks>Any signed-in user with an active account may view this profile. Deleted users are not found. The profile shows whether the caller follows the user and how many users follow them and are followed by them; deleted accounts are not counted. Session and role information is not returned.</remarks>
    /// <param name="userId" example="00000000-0000-0000-0000-000000000001">The user's id.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <response code="200">The user's profile.</response>
    /// <response code="400">The id is empty, or the caller's account details needed to create the local profile are invalid; errors lists the field.</response>
    /// <response code="401">The session is invalid or the caller's account is deleted.</response>
    /// <response code="404">The user does not exist or is deleted.</response>
    [HttpGet("{userId:guid}")]
    [ProducesResponseType(typeof(UserProfileDetailsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProfile(Guid userId, CancellationToken cancellationToken)
    {
        Result<UserProfileDetailsResponse> result = await Sender.Send(new GetUserProfileQuery(userId), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : HandleFailure(result);
    }

    /// <summary>Lists a user's posts, newest first.</summary>
    /// <remarks>
    /// Any signed-in user with an active account may call this endpoint. Deleted posts are excluded.
    /// A first request may create the caller's local account from their sign-in provider details.
    /// To get the next page, pass the previous page's nextCursor as cursor. It is null on the last page.
    /// </remarks>
    /// <param name="userId" example="00000000-0000-0000-0000-000000000001">The author's id.</param>
    /// <param name="cursor" example="NjM5MDI4MjI0MDAwMDAwMDAwXzAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAx">The nextCursor of the previous page. Omit it for the first page.</param>
    /// <param name="limit" example="20">The page size, from 1 to 50. Defaults to 20.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <response code="200">One page of the user's posts. The page can be empty.</response>
    /// <response code="400">The id is empty, the cursor or limit is invalid, or the caller's account details are invalid.</response>
    /// <response code="401">The session is invalid or the caller's account is deleted.</response>
    /// <response code="404">The user does not exist or is deleted.</response>
    [HttpGet("{userId:guid}/posts")]
    [ProducesResponseType(typeof(PageResponse<PostResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPosts(
        Guid userId,
        string? cursor = null,
        int limit = GetMyPostsQuery.DefaultLimit,
        CancellationToken cancellationToken = default)
    {
        Result<PageResponse<PostResponse>> result = await Sender.Send(
            new GetUserPostsQuery(userId, cursor, limit), cancellationToken);

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

    /// <summary>Lists the users who follow a user, most recent follower first.</summary>
    /// <remarks>
    /// Any signed-in user with an active account may call this endpoint. Deleted accounts are excluded.
    /// A first request may create the caller's local account from their sign-in provider details.
    /// To get the next page, pass the previous page's nextCursor as cursor. It is null on the last page.
    /// </remarks>
    /// <param name="userId" example="00000000-0000-0000-0000-000000000001">The followed user's id.</param>
    /// <param name="cursor" example="NjM5MDI4MjI0MDAwMDAwMDAwXzAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAx">The nextCursor of the previous page. Omit it for the first page.</param>
    /// <param name="limit" example="20">The page size, from 1 to 50. Defaults to 20.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <response code="200">One page of followers. The page can be empty.</response>
    /// <response code="400">The id is empty, the cursor or limit is invalid, or the caller's account details are invalid.</response>
    /// <response code="401">The session is invalid or the caller's account is deleted.</response>
    /// <response code="404">The user does not exist or is deleted.</response>
    [HttpGet("{userId:guid}/followers")]
    [ProducesResponseType(typeof(PageResponse<UserSearchItemResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetFollowers(
        Guid userId,
        string? cursor = null,
        int limit = GetUserFollowersQuery.DefaultLimit,
        CancellationToken cancellationToken = default)
    {
        Result<PageResponse<UserSearchItemResponse>> result = await Sender.Send(
            new GetUserFollowersQuery(userId, cursor, limit), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : HandleFailure(result);
    }

    /// <summary>Lists the users a user follows, most recently followed first.</summary>
    /// <remarks>
    /// Any signed-in user with an active account may call this endpoint. Deleted accounts are excluded.
    /// A first request may create the caller's local account from their sign-in provider details.
    /// To get the next page, pass the previous page's nextCursor as cursor. It is null on the last page.
    /// </remarks>
    /// <param name="userId" example="00000000-0000-0000-0000-000000000001">The following user's id.</param>
    /// <param name="cursor" example="NjM5MDI4MjI0MDAwMDAwMDAwXzAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAx">The nextCursor of the previous page. Omit it for the first page.</param>
    /// <param name="limit" example="20">The page size, from 1 to 50. Defaults to 20.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <response code="200">One page of followed users. The page can be empty.</response>
    /// <response code="400">The id is empty, the cursor or limit is invalid, or the caller's account details are invalid.</response>
    /// <response code="401">The session is invalid or the caller's account is deleted.</response>
    /// <response code="404">The user does not exist or is deleted.</response>
    [HttpGet("{userId:guid}/following")]
    [ProducesResponseType(typeof(PageResponse<UserSearchItemResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetFollowing(
        Guid userId,
        string? cursor = null,
        int limit = GetUserFollowersQuery.DefaultLimit,
        CancellationToken cancellationToken = default)
    {
        Result<PageResponse<UserSearchItemResponse>> result = await Sender.Send(
            new GetUserFollowingQuery(userId, cursor, limit), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : HandleFailure(result);
    }

    /// <summary>Follows another user.</summary>
    /// <remarks>Any signed-in user with an active account may call this endpoint. Repeating it when the target is already followed changes nothing.</remarks>
    /// <param name="userId" example="00000000-0000-0000-0000-000000000001">The target user's id.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <response code="204">The caller follows the target user.</response>
    /// <response code="400">The id is empty, the caller tried to follow their own account, or the caller's account details are invalid.</response>
    /// <response code="401">The session is invalid or the caller's account is deleted.</response>
    /// <response code="404">The target user does not exist or is deleted.</response>
    [HttpPut("{userId:guid}/follow")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Follow(Guid userId, CancellationToken cancellationToken)
    {
        Result result = await Sender.Send(new FollowUserCommand(userId), cancellationToken);

        return result.IsSuccess ? NoContent() : HandleFailure(result);
    }

    /// <summary>Stops following another user.</summary>
    /// <remarks>Any signed-in user with an active account may call this endpoint. Repeating it when the target is already unfollowed changes nothing.</remarks>
    /// <param name="userId" example="00000000-0000-0000-0000-000000000001">The target user's id.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <response code="204">The caller does not follow the target user.</response>
    /// <response code="400">The id is empty or the caller's account details are invalid.</response>
    /// <response code="401">The session is invalid or the caller's account is deleted.</response>
    /// <response code="404">The target user does not exist or is deleted.</response>
    [HttpDelete("{userId:guid}/follow")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Unfollow(Guid userId, CancellationToken cancellationToken)
    {
        Result result = await Sender.Send(new UnfollowUserCommand(userId), cancellationToken);

        return result.IsSuccess ? NoContent() : HandleFailure(result);
    }
}
