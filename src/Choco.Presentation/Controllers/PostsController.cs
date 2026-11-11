using Asp.Versioning;

using Choco.Contract.Abstractions;
using Choco.Contract.Comments;
using Choco.Contract.Posts;
using Choco.Presentation.Abstractions;
using Choco.SharedKernel.Results;

using MediatR;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Choco.Presentation.Controllers;

/// <summary>
/// Text posts that users publish.
/// </summary>
[Route("api/v{version:apiVersion}/posts")]
[ApiVersion(1.0)]
public sealed class PostsController : ApiController
{
    public PostsController(ISender sender) : base(sender) { }

    /// <summary>
    /// Publishes a post.
    /// </summary>
    /// <remarks>
    /// Any signed-in user may call it. The new post belongs to the caller and is visible to every signed-in user.
    /// Leading and trailing whitespace is removed from the content. Sending the same body twice publishes two posts.
    /// </remarks>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <param name="command">The post to publish.</param>
    /// <response code="201">The post was published; the Location header points to it.</response>
    /// <response code="400">The content is missing, blank or too long; errors lists the field.</response>
    /// <response code="401">The request has no valid session token, or the caller's account is deleted.</response>
    [HttpPost]
    [ProducesResponseType(typeof(PostResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Create([FromBody] CreatePostCommand command, CancellationToken cancellationToken)
    {
        Result<PostResponse> result = await Sender.Send(command, cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { version = RouteData.Values["version"], id = result.Value.Id }, result.Value)
            : HandleFailure(result);
    }

    /// <summary>
    /// Replaces the text of a post.
    /// </summary>
    /// <remarks>
    /// Only the author may call it. Leading and trailing whitespace is removed from the content.
    /// Sending the same body again leaves the post as it is. A deleted post is not found.
    /// </remarks>
    /// <param name="id" example="00000000-0000-0000-0000-000000000001">The post's id.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <param name="command">The new text of the post.</param>
    /// <response code="200">The post with its new text.</response>
    /// <response code="400">The content is missing, blank or too long; errors lists the field.</response>
    /// <response code="401">The request has no valid session token, or the caller's account is deleted.</response>
    /// <response code="403">The caller is not the author of the post.</response>
    /// <response code="404">No post has this id.</response>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(PostResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Edit(Guid id, [FromBody] EditPostCommand command, CancellationToken cancellationToken)
    {
        Result<PostResponse> result = await Sender.Send(command with { Id = id }, cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : HandleFailure(result);
    }

    /// <summary>
    /// Deletes a post.
    /// </summary>
    /// <remarks>
    /// The author may delete their own post; a caller with the permission to delete any post may delete any post.
    /// A deleted post is no longer found, so deleting it again returns 404.
    /// </remarks>
    /// <param name="id" example="00000000-0000-0000-0000-000000000001">The post's id.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <response code="204">The post was deleted.</response>
    /// <response code="400">The id is empty; errors lists the field.</response>
    /// <response code="401">The request has no valid session token, or the caller's account is deleted.</response>
    /// <response code="403">The caller is not the author and may not delete other users' posts.</response>
    /// <response code="404">No post has this id.</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        Result result = await Sender.Send(new DeletePostCommand(id), cancellationToken);

        return result.IsSuccess ? NoContent() : HandleFailure(result);
    }

    /// <summary>
    /// Likes a post.
    /// </summary>
    /// <remarks>
    /// Any signed-in user may call it, including on their own post. Liking a post the caller already likes changes nothing.
    /// A post that is deleted, or whose author is deleted, is not found.
    /// </remarks>
    /// <param name="id" example="00000000-0000-0000-0000-000000000001">The post's id.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <response code="204">The caller likes the post.</response>
    /// <response code="400">The id is empty; errors lists the field.</response>
    /// <response code="401">The request has no valid session token, or the caller's account is deleted.</response>
    /// <response code="403">The caller blocked the post's author, or the author blocked the caller.</response>
    /// <response code="404">The post does not exist, is deleted, or was written by a deleted account.</response>
    [HttpPut("{id:guid}/like")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Like(Guid id, CancellationToken cancellationToken)
    {
        Result result = await Sender.Send(new LikePostCommand(id), cancellationToken);

        return result.IsSuccess ? NoContent() : HandleFailure(result);
    }

    /// <summary>
    /// Removes the caller's like from a post.
    /// </summary>
    /// <remarks>
    /// Any signed-in user may call it. Removing a like the caller does not have changes nothing.
    /// A post that is deleted, or whose author is deleted, is not found.
    /// </remarks>
    /// <param name="id" example="00000000-0000-0000-0000-000000000001">The post's id.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <response code="204">The caller does not like the post.</response>
    /// <response code="400">The id is empty; errors lists the field.</response>
    /// <response code="401">The request has no valid session token, or the caller's account is deleted.</response>
    /// <response code="404">The post does not exist, is deleted, or was written by a deleted account.</response>
    [HttpDelete("{id:guid}/like")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Unlike(Guid id, CancellationToken cancellationToken)
    {
        Result result = await Sender.Send(new UnlikePostCommand(id), cancellationToken);

        return result.IsSuccess ? NoContent() : HandleFailure(result);
    }

    /// <summary>
    /// Adds a comment to a post.
    /// </summary>
    /// <remarks>
    /// Any signed-in user may call it, including on their own post. Leading and trailing whitespace is removed from the content.
    /// Sending the same body twice adds two comments. A post that is deleted, or whose author is deleted, is not found.
    /// The Location header points to the post's list of comments.
    /// </remarks>
    /// <param name="id" example="00000000-0000-0000-0000-000000000001">The post's id.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <param name="command">The comment to add.</param>
    /// <response code="201">The comment was added.</response>
    /// <response code="400">The content is missing, blank or too long, or the id is empty; errors lists the field.</response>
    /// <response code="401">The request has no valid session token, or the caller's account is deleted.</response>
    /// <response code="403">The caller blocked the post's author, or the author blocked the caller.</response>
    /// <response code="404">The post does not exist, is deleted, or was written by a deleted account.</response>
    [HttpPost("{id:guid}/comments")]
    [ProducesResponseType(typeof(CommentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddComment(
        Guid id, [FromBody] CreateCommentCommand command, CancellationToken cancellationToken)
    {
        Result<CommentResponse> result = await Sender.Send(command with { PostId = id }, cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetComments), new { version = RouteData.Values["version"], id }, result.Value)
            : HandleFailure(result);
    }

    /// <summary>
    /// Lists the comments on a post, oldest first.
    /// </summary>
    /// <remarks>
    /// Any signed-in user may call it. Deleted comments, comments by deleted accounts and comments by users who blocked the caller or whom the caller blocked are not listed.
    /// A post by such a user is not found.
    /// To get the next page, pass the nextCursor of the previous page as cursor; it is null on the last page.
    /// </remarks>
    /// <param name="id" example="00000000-0000-0000-0000-000000000001">The post's id.</param>
    /// <param name="cursor" example="NjM5MDI4MjI0MDAwMDAwMDAwXzAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAx">The nextCursor of the previous page; leave it out for the first page.</param>
    /// <param name="limit" example="20">How many comments to return, from 1 to 50. Defaults to 20.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <response code="200">One page of the post's comments. The page can be empty.</response>
    /// <response code="400">The id is empty, or the limit or cursor is not valid; errors lists the field.</response>
    /// <response code="401">The request has no valid session token, or the caller's account is deleted.</response>
    /// <response code="404">The post does not exist, is deleted, or was written by a deleted account.</response>
    [HttpGet("{id:guid}/comments")]
    [ProducesResponseType(typeof(PageResponse<CommentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetComments(
        Guid id,
        string? cursor = null,
        int limit = GetMyPostsQuery.DefaultLimit,
        CancellationToken cancellationToken = default)
    {
        Result<PageResponse<CommentResponse>> result = await Sender.Send(
            new GetPostCommentsQuery(id, cursor, limit), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : HandleFailure(result);
    }

    /// <summary>
    /// Gets a post by its id.
    /// </summary>
    /// <remarks>
    /// Any signed-in user may call it. A deleted post, or a post by a user who blocked the caller or whom the caller blocked, is not found.
    /// </remarks>
    /// <param name="id" example="00000000-0000-0000-0000-000000000001">The post's id.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <response code="200">The post.</response>
    /// <response code="401">The request has no valid session token.</response>
    /// <response code="404">No post has this id.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PostResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        Result<PostResponse> result = await Sender.Send(new GetPostQuery(id), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : HandleFailure(result);
    }
}
