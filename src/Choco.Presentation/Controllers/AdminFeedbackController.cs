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
    /// <remarks>Only a caller with the permission to read any feedback may call it. Use status, category, q and author to narrow the list; leave them out for all feedback. All filters combine with AND. Searches ignore case and accents (a, á and ả match each other; d and đ match each other) and surrounding whitespace; a blank q or author is ignored. Pages are numbered from 1. The response carries the page, the page size and totalCount, the number of feedback entries that match the filters across all pages. A page past the last one is not an error; its items are empty.</remarks>
    /// <param name="status" example="new">Only feedback in this state: new, in_progress, resolved or wont_fix.</param>
    /// <param name="category" example="bug">Only feedback of this kind: bug, suggestion or other.</param>
    /// <param name="q" example="login error">Words to find in the message, split on whitespace. Every word must appear; only the first 5 words count. At most 100 characters.</param>
    /// <param name="author" example="anh">Part of the sender's username or display name, taken as one phrase. Feedback from deleted accounts never matches. At most 100 characters.</param>
    /// <param name="page" example="1">The page number, from 1. Defaults to 1.</param>
    /// <param name="pageSize" example="20">The page size, from 1 to 50. Defaults to 20.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <response code="200">One page of feedback and the total count. The page can be empty.</response>
    /// <response code="400">The status, category, page or page size is invalid, or q or author is longer than 100 characters.</response>
    /// <response code="401">The session is invalid or the caller's account is deleted.</response>
    /// <response code="403">The caller may not read feedback.</response>
    [HttpGet]
    [ProducesResponseType(typeof(OffsetPageResponse<FeedbackResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAll(
        string? status = null,
        string? category = null,
        string? q = null,
        string? author = null,
        int page = GetFeedbackQuery.DefaultPage,
        int pageSize = GetFeedbackQuery.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        Result<OffsetPageResponse<FeedbackResponse>> result = await Sender.Send(
            new GetFeedbackQuery(status, category, page, pageSize, q, author), cancellationToken);

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
    /// <response code="403">The caller may not change feedback status. It is also returned when the caller's account is suspended.</response>
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
