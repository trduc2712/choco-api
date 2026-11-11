using Asp.Versioning;

using Choco.Contract.AdminCounts;
using Choco.Presentation.Abstractions;
using Choco.SharedKernel.Results;

using MediatR;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Choco.Presentation.Controllers;

/// <summary>
/// Counts of work waiting for administrators, for the admin sidebar.
/// </summary>
[Route("api/v{version:apiVersion}/admin/counts")]
[ApiVersion(1.0)]
public sealed class AdminCountsController : ApiController
{
    public AdminCountsController(ISender sender) : base(sender) { }

    /// <summary>Gets how much feedback and how many reports wait for attention.</summary>
    /// <remarks>A caller who may read feedback gets newFeedback, and a caller who may handle reports gets openReports. A count the caller may not see is null. A caller who may see neither is refused.</remarks>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <response code="200">The counts. A count the caller may not see is null.</response>
    /// <response code="401">The session is invalid or the caller's account is deleted.</response>
    /// <response code="403">The caller may neither read feedback nor handle reports.</response>
    [HttpGet]
    [ProducesResponseType(typeof(AdminCountsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        Result<AdminCountsResponse> result = await Sender.Send(new GetAdminCountsQuery(), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : HandleFailure(result);
    }
}
