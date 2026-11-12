using Asp.Versioning;

using Choco.Contract.Comments;
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
}
