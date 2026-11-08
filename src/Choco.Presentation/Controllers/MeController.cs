using System.Security.Claims;

using Asp.Versioning;

using Choco.Application.Abstractions.Authentication;
using Choco.Contract.Abstractions;
using Choco.Contract.Posts;
using Choco.Contract.Users;
using Choco.Presentation.Abstractions;
using Choco.SharedKernel.Results;

using MediatR;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Choco.Presentation.Controllers;

/// <summary>
/// The signed-in user and what belongs to them.
/// </summary>
[Route("api/v{version:apiVersion}/me")]
[ApiVersion(1.0)]
public sealed class MeController : ApiController
{
    private readonly ICurrentUser _currentUser;

    public MeController(ISender sender, ICurrentUser currentUser) : base(sender)
    {
        _currentUser = currentUser;
    }

    /// <summary>Gets the caller's basic profile.</summary>
    /// <remarks>Any signed-in user may call it. Creates the local account if needed. Returns the stored username, display name and avatar.</remarks>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <response code="200">The caller's profile.</response>
    /// <response code="400">The account details needed to create the local profile are invalid; errors lists the field.</response>
    /// <response code="401">The session is invalid or the caller's account is deleted.</response>
    [HttpGet("profile")]
    [ProducesResponseType(typeof(UserProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetProfile(CancellationToken cancellationToken)
    {
        Result<UserProfileResponse> result = await Sender.Send(new GetMyProfileQuery(), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : HandleFailure(result);
    }

    /// <summary>Replaces the caller's username and display name.</summary>
    /// <remarks>Any signed-in user may call it. The display name combines the first and last names. Sending the same values again is safe.</remarks>
    /// <param name="command">The new username and name.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <response code="200">The updated profile.</response>
    /// <response code="400">One or more profile fields are invalid.</response>
    /// <response code="401">The session is invalid or the caller's account is deleted.</response>
    /// <response code="409">The username is unavailable.</response>
    [HttpPut("profile")]
    [ProducesResponseType(typeof(UserProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateMyProfileCommand command, CancellationToken cancellationToken)
    {
        Result<UserProfileResponse> result = await Sender.Send(command, cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : HandleFailure(result);
    }

    /// <summary>Uploads a new avatar for the caller.</summary>
    /// <remarks>Any signed-in user may call it. Send a multipart/form-data file named image, up to 10 MB. The uploaded image replaces the caller's current avatar.</remarks>
    /// <param name="image">The image file in a multipart/form-data field named image, up to 10 MB.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <response code="200">The profile with the new avatar URL.</response>
    /// <response code="400">The image is missing, too large, or unsupported.</response>
    /// <response code="401">The session is invalid or the caller's account is deleted.</response>
    /// <response code="413">The multipart request exceeds the request size limit.</response>
    /// <response code="415">The request does not use multipart/form-data.</response>
    [HttpPost("profile/avatar")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(UploadMyAvatarCommand.MaxImageBytes + 65_536)]
    [ProducesResponseType(typeof(UserProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status415UnsupportedMediaType)]
    public async Task<IActionResult> UploadAvatar(IFormFile image, CancellationToken cancellationToken)
    {
        await using Stream stream = image.OpenReadStream();
        Result<UserProfileResponse> result = await Sender.Send(new UploadMyAvatarCommand(stream), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : HandleFailure(result);
    }

    /// <summary>Removes the caller's avatar.</summary>
    /// <remarks>Any signed-in user may call it. The caller may repeat the request when they have no avatar.</remarks>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <response code="200">The profile with a null avatar URL.</response>
    /// <response code="401">The session is invalid or the caller's account is deleted.</response>
    [HttpDelete("profile/avatar")]
    [ProducesResponseType(typeof(UserProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RemoveAvatar(CancellationToken cancellationToken)
    {
        Result<UserProfileResponse> result = await Sender.Send(new RemoveMyAvatarCommand(), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : HandleFailure(result);
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        Result<Guid> userIdResult = await _currentUser.GetIdAsync(cancellationToken);

        if (userIdResult.IsFailure)
        {
            return HandleFailure(userIdResult);
        }

        return Ok(new
        {
            id = userIdResult.Value,
            identityId = User.FindFirstValue(JwtRegisteredClaimNames.Sub),
            sessionId = User.FindFirstValue(JwtRegisteredClaimNames.Sid),
            authorizedParty = User.FindFirstValue(JwtRegisteredClaimNames.Azp)
        });
    }

    /// <summary>
    /// Lists the caller's own posts, newest first.
    /// </summary>
    /// <remarks>
    /// Any signed-in user may call it. Deleted posts are not listed.
    /// To get the next page, pass the nextCursor of the previous page as cursor; it is null on the last page.
    /// </remarks>
    /// <param name="cursor" example="NjM5MDI4MjI0MDAwMDAwMDAwXzAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAx">The nextCursor of the previous page; leave it out for the first page.</param>
    /// <param name="limit" example="20">How many posts to return, from 1 to 50. Defaults to 20.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <response code="200">One page of the caller's posts.</response>
    /// <response code="400">The limit is out of range or the cursor is not valid; errors lists the field.</response>
    /// <response code="401">The request has no valid session token, or the caller's account is deleted.</response>
    [HttpGet("posts")]
    [ProducesResponseType(typeof(PageResponse<PostResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetPosts(
        string? cursor = null,
        int limit = GetMyPostsQuery.DefaultLimit,
        CancellationToken cancellationToken = default)
    {
        Result<PageResponse<PostResponse>> result = await Sender.Send(new GetMyPostsQuery(cursor, limit), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : HandleFailure(result);
    }

    /// <summary>
    /// Lists posts from the accounts the caller follows, newest first.
    /// </summary>
    /// <remarks>
    /// Any signed-in user may call it. The caller's own posts, deleted posts and posts by deleted accounts are not listed.
    /// A caller who follows nobody gets an empty page.
    /// To get the next page, pass the nextCursor of the previous page as cursor; it is null on the last page.
    /// </remarks>
    /// <param name="cursor" example="NjM5MDI4MjI0MDAwMDAwMDAwXzAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAx">The nextCursor of the previous page; leave it out for the first page.</param>
    /// <param name="limit" example="20">How many posts to return, from 1 to 50. Defaults to 20.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <response code="200">One page of posts from followed accounts. The page can be empty.</response>
    /// <response code="400">The limit is out of range or the cursor is not valid; errors lists the field.</response>
    /// <response code="401">The request has no valid session token, or the caller's account is deleted.</response>
    [HttpGet("feed/following")]
    [ProducesResponseType(typeof(PageResponse<PostResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetFollowingFeed(
        string? cursor = null,
        int limit = GetMyPostsQuery.DefaultLimit,
        CancellationToken cancellationToken = default)
    {
        Result<PageResponse<PostResponse>> result = await Sender.Send(
            new GetFollowingFeedQuery(cursor, limit), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : HandleFailure(result);
    }
}
