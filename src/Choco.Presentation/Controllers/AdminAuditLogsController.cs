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
    /// <remarks>Only a caller with the permission to read the audit log may call it. Entries cannot be changed or deleted through the API. Use the filters to narrow the list; leave them out for every entry. A time range includes from and excludes to. To get the next page, pass the previous page's nextCursor as cursor. It is null on the last page. The source address and user agent of a request are not part of the response.</remarks>
    /// <param name="actorId" example="00000000-0000-0000-0000-000000000001">Only entries made by this user.</param>
    /// <param name="action" example="PostDeleted">Only entries of this action, such as PostDeleted or RoleAssigned.</param>
    /// <param name="targetType" example="Post">Only entries about this kind of object: Post, Comment, User, Report or Feedback.</param>
    /// <param name="targetId" example="00000000-0000-0000-0000-000000000002">Only entries about the object with this id.</param>
    /// <param name="affectedUserId" example="00000000-0000-0000-0000-000000000003">Only entries that affected this user.</param>
    /// <param name="from" example="2026-01-01T00:00:00+00:00">Only entries at or after this time.</param>
    /// <param name="to" example="2026-02-01T00:00:00+00:00">Only entries before this time. It must be later than from.</param>
    /// <param name="cursor" example="MTIzNDU2Nzg5MF8wMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMQ">The previous page's nextCursor; leave it out for the first page.</param>
    /// <param name="limit" example="20">The page size, from 1 to 50. Defaults to 20.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <response code="200">One page of entries. The page can be empty.</response>
    /// <response code="400">A filter, the cursor or the limit is invalid.</response>
    /// <response code="401">The session is invalid or the caller's account is deleted.</response>
    /// <response code="403">The caller may not read the audit log.</response>
    [HttpGet]
    [ProducesResponseType(typeof(PageResponse<AuditLogResponse>), StatusCodes.Status200OK)]
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
        string? cursor = null,
        int limit = GetAuditLogsQuery.DefaultLimit,
        CancellationToken cancellationToken = default)
    {
        Result<PageResponse<AuditLogResponse>> result = await Sender.Send(
            new GetAuditLogsQuery(actorId, action, targetType, targetId, affectedUserId, from, to, cursor, limit),
            cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : HandleFailure(result);
    }
}
