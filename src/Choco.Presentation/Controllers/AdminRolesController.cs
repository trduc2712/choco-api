using Asp.Versioning;

using Choco.Contract.Roles;
using Choco.Presentation.Abstractions;
using Choco.SharedKernel.Results;

using MediatR;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Choco.Presentation.Controllers;

/// <summary>
/// Roles and the permissions they hold, for administrators.
/// </summary>
[Route("api/v{version:apiVersion}/admin/roles")]
[ApiVersion(1.0)]
public sealed class AdminRolesController : ApiController
{
    public AdminRolesController(ISender sender) : base(sender) { }

    /// <summary>Lists every role with the permissions it holds.</summary>
    /// <remarks>Only a caller with the permission to assign roles may call it. Roles come ordered by name, and each role's permissions are in alphabetical order. The response is not paged. Use a role's id to assign or revoke it on a user.</remarks>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <response code="200">Every role, with its id, key, name, whether it is the default role and its permissions.</response>
    /// <response code="401">The session is invalid or the caller's account is deleted.</response>
    /// <response code="403">The caller may not assign roles.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<RoleResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<RoleResponse>> result = await Sender.Send(new GetRolesQuery(), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : HandleFailure(result);
    }
}
