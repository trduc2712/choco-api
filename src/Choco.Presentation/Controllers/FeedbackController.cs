using Asp.Versioning;

using Choco.Contract.Feedback;
using Choco.Presentation.Abstractions;
using Choco.Presentation.RateLimiting;
using Choco.SharedKernel.Results;

using MediatR;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Choco.Presentation.Controllers;

/// <summary>
/// Feedback about the app from its users.
/// </summary>
[Route("api/v{version:apiVersion}/feedback")]
[ApiVersion(1.0)]
public sealed class FeedbackController : ApiController
{
    public FeedbackController(ISender sender) : base(sender) { }

    /// <summary>Sends feedback about the app.</summary>
    /// <remarks>Any signed-in user with an active account may call this endpoint. The feedback is saved as new and credited to the caller; the body cannot name another author. A caller may send 5 feedback per hour and 20 per day. Over the limit, the response is 429 and its Retry-After header says how many seconds to wait. Sending the same feedback twice saves it twice.</remarks>
    /// <param name="command">The kind of feedback, the message and where the user was in the app.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <response code="201">The feedback was saved.</response>
    /// <response code="400">A field is invalid; errors lists the fields, such as Message.</response>
    /// <response code="401">The session is invalid or the caller's account is deleted.</response>
    /// <response code="429">The caller sent too much feedback; wait the number of seconds in the Retry-After header.</response>
    [HttpPost]
    [FeedbackSubmissionLimit]
    [ProducesResponseType(typeof(CreatedFeedbackResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Create([FromBody] CreateFeedbackCommand command, CancellationToken cancellationToken)
    {
        Result<CreatedFeedbackResponse> result = await Sender.Send(command, cancellationToken);

        return result.IsSuccess ? StatusCode(StatusCodes.Status201Created, result.Value) : HandleFailure(result);
    }
}
