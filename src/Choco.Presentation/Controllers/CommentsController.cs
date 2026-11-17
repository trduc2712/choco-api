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
/// Comments that users write on posts.
/// </summary>
[Route("api/v{version:apiVersion}/comments")]
[ApiVersion(1.0)]
public sealed class CommentsController : ApiController
{
    public CommentsController(ISender sender) : base(sender) { }

    /// <summary>
    /// Deletes a comment.
    /// </summary>
    /// <remarks>
    /// The author may delete their own comment; a caller with the permission to delete any comment may delete any comment.
    /// A deleted comment is no longer found, so deleting it again returns 404. A comment on a post that is deleted, or whose author is deleted, is not found either.
    /// </remarks>
    /// <param name="id" example="00000000-0000-0000-0000-000000000001">The comment's id.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <response code="204">The comment was deleted.</response>
    /// <response code="400">The id is empty; errors lists the field.</response>
    /// <response code="401">The request has no valid session token, or the caller's account is deleted.</response>
    /// <response code="403">The caller is not the author and may not delete other users' comments. It is also returned when the caller's account is suspended.</response>
    /// <response code="404">No comment has this id.</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        Result result = await Sender.Send(new DeleteCommentCommand(id), cancellationToken);

        return result.IsSuccess ? NoContent() : HandleFailure(result);
    }

    /// <summary>
    /// Sets the caller's reaction to a comment.
    /// </summary>
    /// <remarks>
    /// Any signed-in user may call it, including on their own comment. A user has at most one reaction per comment, so sending the other type changes the reaction, and sending the same type again changes nothing.
    /// The author of the comment is notified the first time the caller likes it, unless the caller wrote it.
    /// </remarks>
    /// <param name="id" example="00000000-0000-0000-0000-000000000001">The comment's id.</param>
    /// <param name="command">The reaction to set.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <response code="204">The caller's reaction is now the one sent.</response>
    /// <response code="400">The type is not like or dislike, or the id is empty; errors lists the field.</response>
    /// <response code="401">The request has no valid session token, or the caller's account is deleted.</response>
    /// <response code="403">The caller and the post's author, or the caller and the comment's author, blocked each other. It is also returned when the caller's account is suspended.</response>
    /// <response code="404">The comment does not exist, is deleted, or was written by a deleted account, or its post is not found.</response>
    [HttpPut("{id:guid}/reaction")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetReaction(
        Guid id, [FromBody] SetCommentReactionCommand command, CancellationToken cancellationToken)
    {
        Result result = await Sender.Send(command with { CommentId = id }, cancellationToken);

        return result.IsSuccess ? NoContent() : HandleFailure(result);
    }

    /// <summary>
    /// Removes the caller's reaction from a comment.
    /// </summary>
    /// <remarks>
    /// Any signed-in user may call it. It succeeds when the caller has no reaction to the comment, so calling it again changes nothing.
    /// </remarks>
    /// <param name="id" example="00000000-0000-0000-0000-000000000001">The comment's id.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <response code="204">The caller has no reaction to the comment.</response>
    /// <response code="400">The id is empty; errors lists the field.</response>
    /// <response code="401">The request has no valid session token, or the caller's account is deleted.</response>
    /// <response code="403">The caller's account is suspended.</response>
    /// <response code="404">The comment does not exist, is deleted, or was written by a deleted account, or its post is not found.</response>
    [HttpDelete("{id:guid}/reaction")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveReaction(Guid id, CancellationToken cancellationToken)
    {
        Result result = await Sender.Send(new RemoveCommentReactionCommand(id), cancellationToken);

        return result.IsSuccess ? NoContent() : HandleFailure(result);
    }

    /// <summary>
    /// Replies to a comment.
    /// </summary>
    /// <remarks>
    /// Any signed-in user may call it, including on their own comment. The reply belongs to the same post as the comment it answers.
    /// A reply can itself be replied to, down to 5 levels below a comment written on the post. Leading and trailing whitespace is removed from the content.
    /// Sending the same body twice adds two replies. The author of the comment being answered is notified, unless they wrote the reply.
    /// The Location header points to the comment's list of replies.
    /// </remarks>
    /// <param name="id" example="00000000-0000-0000-0000-000000000001">The id of the comment to reply to.</param>
    /// <param name="command">The reply to add.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <response code="201">The reply was added.</response>
    /// <response code="400">The content is missing, blank or too long, or the id is empty; errors lists the field. It is also returned when the comment is already 5 levels deep.</response>
    /// <response code="401">The request has no valid session token, or the caller's account is deleted.</response>
    /// <response code="403">The caller and the post's author, or the caller and the comment's author, blocked each other. It is also returned when the caller's account is suspended.</response>
    /// <response code="404">The comment does not exist, is deleted, or was written by a deleted account, or its post is not found.</response>
    [HttpPost("{id:guid}/replies")]
    [ProducesResponseType(typeof(CommentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Reply(
        Guid id, [FromBody] ReplyToCommentCommand command, CancellationToken cancellationToken)
    {
        Result<CommentResponse> result = await Sender.Send(command with { CommentId = id }, cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetReplies), new { version = RouteData.Values["version"], id }, result.Value)
            : HandleFailure(result);
    }

    /// <summary>
    /// Lists the direct replies to a comment, oldest first.
    /// </summary>
    /// <remarks>
    /// Any signed-in user may call it. Each reply has its replyCount; list the replies of a reply with this endpoint again.
    /// A reply that is deleted, was written by a deleted account, or was written by a user who blocked the caller or whom the caller blocked is listed as a placeholder when it has replies, and is not listed otherwise.
    /// The replies of a placeholder can be listed too. A comment on a post that is not found, or on a post by a user who blocked the caller or whom the caller blocked, is not found.
    /// To get the next page, pass the nextCursor of the previous page as cursor; it is null on the last page.
    /// </remarks>
    /// <param name="id" example="00000000-0000-0000-0000-000000000001">The comment's id.</param>
    /// <param name="cursor" example="NjM5MDI4MjI0MDAwMDAwMDAwXzAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAx">The nextCursor of the previous page; leave it out for the first page.</param>
    /// <param name="limit" example="20">How many replies to return, from 1 to 50. Defaults to 20.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <response code="200">One page of the comment's direct replies. The page can be empty.</response>
    /// <response code="400">The id is empty, or the limit or cursor is not valid; errors lists the field.</response>
    /// <response code="401">The request has no valid session token, or the caller's account is deleted.</response>
    /// <response code="404">No comment has this id, or its post is not found.</response>
    [HttpGet("{id:guid}/replies")]
    [ProducesResponseType(typeof(PageResponse<CommentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetReplies(
        Guid id,
        string? cursor = null,
        int limit = GetMyPostsQuery.DefaultLimit,
        CancellationToken cancellationToken = default)
    {
        Result<PageResponse<CommentResponse>> result = await Sender.Send(
            new GetCommentRepliesQuery(id, cursor, limit), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : HandleFailure(result);
    }
}
