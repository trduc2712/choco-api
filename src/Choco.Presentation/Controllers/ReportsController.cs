using Asp.Versioning;

using Choco.Contract.Abstractions;
using Choco.Contract.Reports;
using Choco.Presentation.Abstractions;
using Choco.SharedKernel.Results;

using MediatR;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Choco.Presentation.Controllers;

/// <summary>
/// Reports about posts and users, handled by administrators.
/// </summary>
[Route("api/v{version:apiVersion}/reports")]
[ApiVersion(1.0)]
public sealed class ReportsController : ApiController
{
    public ReportsController(ISender sender) : base(sender) { }

    /// <summary>Lists the open reports.</summary>
    /// <remarks>Only a caller with the permission to handle reports may call it. Reports come oldest first. Each shows the target, the reason, the reporter and when it was made. A resolved report no longer appears. Pages are numbered from 1. The response carries the page, the page size and totalCount, the number of open reports across all pages. A page past the last one is not an error; its items are empty.</remarks>
    /// <param name="page" example="1">The page number, from 1. Defaults to 1.</param>
    /// <param name="pageSize" example="20">The page size, from 1 to 50. Defaults to 20.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <response code="200">One page of open reports and the total count. The page can be empty.</response>
    /// <response code="400">The page or the page size is invalid.</response>
    /// <response code="401">The request has no valid session token, or the caller's account is deleted.</response>
    /// <response code="403">The caller may not handle reports.</response>
    [HttpGet]
    [ProducesResponseType(typeof(OffsetPageResponse<ReportResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetOpen(
        int page = GetReportsQuery.DefaultPage,
        int pageSize = GetReportsQuery.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        Result<OffsetPageResponse<ReportResponse>> result = await Sender.Send(
            new GetReportsQuery(page, pageSize), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : HandleFailure(result);
    }

    /// <summary>Resolves a report and carries out its outcome.</summary>
    /// <remarks>Only a caller with the permission to handle reports may call it. The outcome says what was done, and it is done in the same request. Dismissed closes the report and changes nothing else. ContentRemoved deletes the reported post and needs the permission to delete any post; it is refused for a report about a user. UserSuspended suspends the reported user, or the author of the reported post, with "Reported for" and the report's reason as the reason; it needs the permission to suspend users and follows the same rules as suspending a user directly. Resolving a report also resolves every other open report about the same post or user, with the same outcome. If the post is already deleted, or the user is already suspended, the outcome counts as done and the reports are closed. If the action cannot be carried out, nothing changes and the reports stay open. Resolving a report that is already resolved changes nothing, and the first outcome stays.</remarks>
    /// <param name="reportId" example="00000000-0000-0000-0000-000000000001">The report's id.</param>
    /// <param name="command">The outcome: Dismissed, ContentRemoved or UserSuspended.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <response code="204">The report, and every open report about the same target, is resolved and the outcome is carried out.</response>
    /// <response code="400">The outcome is not one of the listed values, ContentRemoved was chosen for a report about a user, or the caller is suspending themselves.</response>
    /// <response code="401">The request has no valid session token, or the caller's account is deleted.</response>
    /// <response code="403">The caller may not handle reports, or lacks the permission or a permission of the user that the outcome needs. It is also returned when the caller's account is suspended.</response>
    /// <response code="404">No report has this id, or the user to suspend no longer exists.</response>
    /// <response code="409">UserSuspended was chosen for the last administrator who is not suspended.</response>
    [HttpPut("{reportId:guid}/resolution")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Resolve(
        Guid reportId, [FromBody] ResolveReportCommand command, CancellationToken cancellationToken)
    {
        Result result = await Sender.Send(command with { ReportId = reportId }, cancellationToken);

        return result.IsSuccess ? NoContent() : HandleFailure(result);
    }
}
