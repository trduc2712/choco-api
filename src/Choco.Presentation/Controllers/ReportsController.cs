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

    /// <summary>Resolves a report.</summary>
    /// <remarks>Only a caller with the permission to handle reports may call it. Resolving a report that is already resolved changes nothing, and the first outcome stays. Acting on the target, such as deleting the post, is a separate request.</remarks>
    /// <param name="reportId" example="00000000-0000-0000-0000-000000000001">The report's id.</param>
    /// <param name="command">The outcome: Dismissed or ActionTaken.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <response code="204">The report is resolved.</response>
    /// <response code="400">The outcome is not one of the listed values.</response>
    /// <response code="401">The request has no valid session token, or the caller's account is deleted.</response>
    /// <response code="403">The caller may not handle reports.</response>
    /// <response code="404">No report has this id.</response>
    [HttpPut("{reportId:guid}/resolution")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Resolve(
        Guid reportId, [FromBody] ResolveReportCommand command, CancellationToken cancellationToken)
    {
        Result result = await Sender.Send(command with { ReportId = reportId }, cancellationToken);

        return result.IsSuccess ? NoContent() : HandleFailure(result);
    }
}
