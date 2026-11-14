using Asp.Versioning;

using Choco.Contract.Abstractions;
using Choco.Contract.AuditLogs;
using Choco.Presentation.Abstractions;
using Choco.SharedKernel.Results;

using MediatR;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Choco.Presentation.Controllers;

/// <summary>
/// The record of who did what to which object, read by administrators.
/// </summary>
[Route("api/v{version:apiVersion}/admin/audit-logs")]
[ApiVersion(1.0)]
public sealed class AdminAuditLogsController : ApiController
{
    public AdminAuditLogsController(ISender sender) : base(sender) { }

    /// <summary>Lists audit log entries, newest first.</summary>
    /// <remarks>Only a caller with the permission to read the audit log may call it. Entries cannot be changed or deleted through the API. Use the filters to narrow the list; leave them out for every entry. A time range includes from and excludes to. Pages are numbered from 1. The response carries the page, the page size and totalCount, the number of entries that match the filters across all pages. A page past the last one is not an error; its items are empty. Each entry includes a changes list. Entries for content and personal data include only the field name. Role permissions are listed as codes added and removed. The source address and user agent of a request are not part of the response. A read of the audit log is recorded as AuditLogRead, with the reader as target, unless the same reader already has an AuditLogRead from the last 5 minutes, so reading many pages or refreshing leaves one entry per reader and window, and a request refused for lack of permission is recorded as AccessDenied, with the caller as target and the refused request in the detail; both can be used as the action filter.</remarks>
    /// <param name="actorId" example="00000000-0000-0000-0000-000000000001">Only entries made by this user.</param>
    /// <param name="action" example="PostDeleted">Only entries of this action, such as PostDeleted or RoleAssigned.</param>
    /// <param name="targetType" example="Post">Only entries about this kind of object: Post, Comment, User, Report, Feedback, Role, Request or AuditLog.</param>
    /// <param name="targetId" example="00000000-0000-0000-0000-000000000002">Only entries about the object with this id.</param>
    /// <param name="affectedUserId" example="00000000-0000-0000-0000-000000000003">Only entries that affected this user.</param>
    /// <param name="from" example="2026-01-01T00:00:00+00:00">Only entries at or after this time.</param>
    /// <param name="to" example="2026-02-01T00:00:00+00:00">Only entries before this time. It must be later than from.</param>
    /// <param name="page" example="1">The page number, from 1. Defaults to 1.</param>
    /// <param name="pageSize" example="20">The page size, from 1 to 50. Defaults to 20.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <response code="200">One page of entries and the total count. The page can be empty.</response>
    /// <response code="400">A filter, the page or the page size is invalid.</response>
    /// <response code="401">The session is invalid or the caller's account is deleted.</response>
    /// <response code="403">The caller may not read the audit log.</response>
    [HttpGet]
    [ProducesResponseType(typeof(OffsetPageResponse<AuditLogResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAll(
        Guid? actorId = null,
        string? action = null,
        string? targetType = null,
        Guid? targetId = null,
        Guid? affectedUserId = null,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        int page = GetAuditLogsQuery.DefaultPage,
        int pageSize = GetAuditLogsQuery.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        Result<OffsetPageResponse<AuditLogResponse>> result = await Sender.Send(
            new GetAuditLogsQuery(actorId, action, targetType, targetId, affectedUserId, from, to, page, pageSize),
            cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : HandleFailure(result);
    }
}
