using System.Security.Claims;

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
[Route("api/me")]
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
}
