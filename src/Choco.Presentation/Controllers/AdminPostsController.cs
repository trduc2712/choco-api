using Asp.Versioning;

using Choco.Contract.Abstractions;
using Choco.Contract.Posts;
using Choco.Presentation.Abstractions;
using Choco.SharedKernel.Results;

using MediatR;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Choco.Presentation.Controllers;

/// <summary>
/// Posts as administrators see them: deleted posts included.
/// </summary>
[Route("api/v{version:apiVersion}/admin/posts")]
[ApiVersion(1.0)]
public sealed class AdminPostsController : ApiController
{
    public AdminPostsController(ISender sender) : base(sender) { }

    /// <summary>Lists posts, newest first.</summary>
    /// <remarks>Only a caller with the permission to read any content may call it. Use state, authorId, from, to and q to narrow the list; leave them out for all visible posts. All filters combine with AND. Deleted posts are left out unless state is deleted, so a post that a report or an audit log entry points to can still be found. Posts of blocked or deleted users are listed too. Searches ignore case and accents (a, á and ả match each other; d and đ match each other) and surrounding whitespace; a blank q is ignored. A time range includes from and excludes to. Pages are numbered from 1. The response carries the page, the page size and totalCount, the number of posts that match the filters across all pages. A page past the last one is not an error; its items are empty.</remarks>
    /// <param name="state" example="visible">Only posts in this state: visible or deleted. Defaults to visible.</param>
    /// <param name="authorId" example="00000000-0000-0000-0000-000000000001">Only posts written by this user.</param>
    /// <param name="from" example="2026-01-01T00:00:00+00:00">Only posts created at or after this time.</param>
    /// <param name="to" example="2026-02-01T00:00:00+00:00">Only posts created before this time. It must be later than from.</param>
    /// <param name="q" example="hello world">Words to find in the content, split on whitespace. Every word must appear; only the first 5 words count. At most 100 characters.</param>
    /// <param name="page" example="1">The page number, from 1. Defaults to 1.</param>
    /// <param name="pageSize" example="20">The page size, from 1 to 50. Defaults to 20.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <response code="200">One page of posts and the total count. The page can be empty.</response>
    /// <response code="400">The state, author id, time range, page or page size is invalid, or q is longer than 100 characters.</response>
    /// <response code="401">The session is invalid or the caller's account is deleted.</response>
    /// <response code="403">The caller may not read all content.</response>
    [HttpGet]
    [ProducesResponseType(typeof(OffsetPageResponse<AdminPostResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAll(
        string? state = null,
        Guid? authorId = null,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        string? q = null,
        int page = GetAdminPostsQuery.DefaultPage,
        int pageSize = GetAdminPostsQuery.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        Result<OffsetPageResponse<AdminPostResponse>> result = await Sender.Send(
            new GetAdminPostsQuery(state, authorId, from, to, page, pageSize, q), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : HandleFailure(result);
    }
}
