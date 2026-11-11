using Asp.Versioning;

using Choco.Contract.Abstractions;
using Choco.Contract.Feedback;
using Choco.Presentation.Abstractions;
using Choco.SharedKernel.Results;

using MediatR;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Choco.Presentation.Controllers;

/// <summary>
/// Feedback about the app, read and triaged by administrators.
/// </summary>
[Route("api/v{version:apiVersion}/admin/feedback")]
[ApiVersion(1.0)]
public sealed class AdminFeedbackController : ApiController
{
    public AdminFeedbackController(ISender sender) : base(sender) { }

    /// <summary>Lists feedback, newest first.</summary>
    /// <remarks>Only a caller with the permission to read any feedback may call it. Use status and category to narrow the list; leave them out for all feedback. To get the next page, pass the previous page's nextCursor as cursor. It is null on the last page.</remarks>
    /// <param name="status" example="new">Only feedback in this state: new, in_progress, resolved or wont_fix.</param>
    /// <param name="category" example="bug">Only feedback of this kind: bug, suggestion or other.</param>
    /// <param name="cursor" example="MTIzNDU2Nzg5MF8wMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMQ">The previous page's nextCursor; leave it out for the first page.</param>
    /// <param name="limit" example="20">The page size, from 1 to 50. Defaults to 20.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <response code="200">One page of feedback. The page can be empty.</response>
    /// <response code="400">The status, category, cursor or limit is invalid.</response>
    /// <response code="401">The session is invalid or the caller's account is deleted.</response>
    /// <response code="403">The caller may not read feedback.</response>
    [HttpGet]
    [ProducesResponseType(typeof(PageResponse<FeedbackResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAll(
        string? status = null,
        string? category = null,
        string? cursor = null,
        int limit = GetFeedbackQuery.DefaultLimit,
        CancellationToken cancellationToken = default)
    {
        Result<PageResponse<FeedbackResponse>> result = await Sender.Send(
            new GetFeedbackQuery(status, category, cursor, limit), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : HandleFailure(result);
    }

    /// <summary>Gets one piece of feedback.</summary>
    /// <remarks>Only a caller with the permission to read any feedback may call it.</remarks>
    /// <param name="id" example="00000000-0000-0000-0000-000000000001">The feedback's id.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <response code="200">The feedback.</response>
    /// <response code="400">The id is empty.</response>
    /// <response code="401">The session is invalid or the caller's account is deleted.</response>
    /// <response code="403">The caller may not read feedback.</response>
    /// <response code="404">No feedback has this id.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(FeedbackResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        Result<FeedbackResponse> result = await Sender.Send(new GetFeedbackByIdQuery(id), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : HandleFailure(result);
    }

    /// <summary>Changes the state of a piece of feedback.</summary>
    /// <remarks>Only a caller with the permission to change feedback status may call it. Sending the state the feedback already has changes nothing, so the call is safe to repeat. The response is the updated feedback.</remarks>
    /// <param name="id" example="00000000-0000-0000-0000-000000000001">The feedback's id.</param>
    /// <param name="command">The new state: new, in_progress, resolved or wont_fix.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <response code="200">The updated feedback.</response>
    /// <response code="400">The id is empty or the status is not one of the listed values.</response>
    /// <response code="401">The session is invalid or the caller's account is deleted.</response>
    /// <response code="403">The caller may not change feedback status.</response>
    /// <response code="404">No feedback has this id.</response>
    [HttpPatch("{id:guid}")]
    [ProducesResponseType(typeof(FeedbackResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStatus(
        Guid id, [FromBody] UpdateFeedbackStatusCommand command, CancellationToken cancellationToken)
    {
        Result<FeedbackResponse> result = await Sender.Send(command with { Id = id }, cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : HandleFailure(result);
    }
}
