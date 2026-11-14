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
/// Individual reports, open and resolved, read by moderators.
/// </summary>
[Route("api/v{version:apiVersion}/admin/reports")]
[ApiVersion(1.0)]
public sealed class AdminReportsController : ApiController
{
    public AdminReportsController(ISender sender) : base(sender) { }

    /// <summary>Lists individual reports, open and resolved, newest first.</summary>
    /// <remarks>Only a caller with the permission to handle reports may call it. It lists one entry for each report, unlike the moderation queue at GET /reports, which shows one row for each post or user with open reports and only the 10 oldest reports behind each row. Use the target id filter to read every report about one post or user, and the status filter to read the reports that were already resolved. Entries come newest first by the time the report was made. Reports about a post or user that was deleted since are still listed. A resolved report shows its outcome, the user who resolved it and the time; resolving a report resolves every open report about the same target, so those reports share the outcome, the resolver and the time. The username of a reporter or resolver whose account is deleted is null. Pages are numbered from 1. The response carries the page, the page size and totalCount, the number of reports that match the filters across all pages. A page past the last one is not an error; its items are empty.</remarks>
    /// <param name="targetType" example="Post">Only reports about this kind of target: Post or User.</param>
    /// <param name="targetId" example="00000000-0000-0000-0000-000000000001">Only reports about the post or user with this id.</param>
    /// <param name="status" example="Resolved">Only reports in this status: Open or Resolved.</param>
    /// <param name="reason" example="Spam">Only reports that give this reason: Spam, Harassment, HateSpeech, Violence or Other.</param>
    /// <param name="outcome" example="ContentRemoved">Only resolved reports with this outcome: Dismissed, ContentRemoved or UserSuspended.</param>
    /// <param name="page" example="1">The page number, from 1. Defaults to 1.</param>
    /// <param name="pageSize" example="20">The page size, from 1 to 50. Defaults to 20.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <response code="200">One page of reports and the total count. The page can be empty.</response>
    /// <response code="400">A filter, the page or the page size is invalid.</response>
    /// <response code="401">The request has no valid session token, or the caller's account is deleted.</response>
    /// <response code="403">The caller may not handle reports.</response>
    [HttpGet]
    [ProducesResponseType(typeof(OffsetPageResponse<AdminReportResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAll(
        string? targetType = null,
        Guid? targetId = null,
        string? status = null,
        string? reason = null,
        string? outcome = null,
        int page = GetAdminReportsQuery.DefaultPage,
        int pageSize = GetAdminReportsQuery.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        Result<OffsetPageResponse<AdminReportResponse>> result = await Sender.Send(
            new GetAdminReportsQuery(targetType, targetId, status, reason, outcome, page, pageSize),
            cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : HandleFailure(result);
    }
}
