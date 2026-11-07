using Asp.Versioning;

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
    /// Gets a post by its id.
    /// </summary>
    /// <remarks>
    /// Any signed-in user may call it. A deleted post is not found.
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
