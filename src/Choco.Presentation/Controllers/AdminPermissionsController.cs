using Asp.Versioning;

using Choco.Contract.Permissions;
using Choco.Presentation.Abstractions;
using Choco.SharedKernel.Results;

using MediatR;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Choco.Presentation.Controllers;

/// <summary>
/// The permissions a role can hold, for administrators.
/// </summary>
[Route("api/v{version:apiVersion}/admin/permissions")]
[ApiVersion(1.0)]
public sealed class AdminPermissionsController : ApiController
{
    public AdminPermissionsController(ISender sender) : base(sender) { }

    /// <summary>Lists every permission a role can hold.</summary>
    /// <remarks>Only a caller with the permission to assign roles may call it. The list is fixed by the application, so it changes only with a new release. Permissions come in alphabetical order by code and the response is not paged.</remarks>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <response code="200">Every permission, with its code.</response>
    /// <response code="401">The session is invalid or the caller's account is deleted.</response>
    /// <response code="403">The caller may not assign roles.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<PermissionResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<PermissionResponse>> result = await Sender.Send(new GetPermissionsQuery(), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : HandleFailure(result);
    }
}
