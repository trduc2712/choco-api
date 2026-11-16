using Asp.Versioning;

using Choco.Contract.Messages;
using Choco.Presentation.Abstractions;
using Choco.SharedKernel.Results;

using MediatR;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Choco.Presentation.Controllers;

/// <summary>
/// Messages of direct conversations between two users.
/// </summary>
[Route("api/v{version:apiVersion}/messages")]
[ApiVersion(1.0)]
public sealed class MessagesController : ApiController
{
    public MessagesController(ISender sender) : base(sender) { }

    /// <summary>Gets one message.</summary>
    /// <remarks>Only the two users of the message's conversation may read it, even when one of them blocked the other or deleted their account. For anyone else the message is not found.</remarks>
    /// <param name="id" example="00000000-0000-0000-0000-000000000001">The message's id.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <response code="200">The message.</response>
    /// <response code="400">The id is empty.</response>
    /// <response code="401">The session is invalid or the caller's account is deleted.</response>
    /// <response code="404">No message has this id, or the caller is not one of the two users of its conversation.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        Result<MessageResponse> result = await Sender.Send(new GetMessageQuery(id), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : HandleFailure(result);
    }
}
