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
    /// <remarks>Only a caller with the permission to read roles may call it. Roles come ordered by name, and each role's permissions are in alphabetical order. The response is not paged. Use a role's id to assign or revoke it on a user.</remarks>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <response code="200">Every role, with its id, key, name, whether it is the default role and its permissions.</response>
    /// <response code="401">The session is invalid or the caller's account is deleted.</response>
    /// <response code="403">The caller may not read roles.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<RoleResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<RoleResponse>> result = await Sender.Send(new GetRolesQuery(), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : HandleFailure(result);
    }

    /// <summary>Gets one role with the permissions it holds.</summary>
    /// <remarks>Only a caller with the permission to read roles may call it. The role's permissions are in alphabetical order.</remarks>
    /// <param name="id" example="00000000-0000-0000-0000-000000000001">The role's id.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <response code="200">The role, with its id, key, name, whether it is the default role and its permissions.</response>
    /// <response code="400">The id is empty.</response>
    /// <response code="401">The session is invalid or the caller's account is deleted.</response>
    /// <response code="403">The caller may not read roles.</response>
    /// <response code="404">No role has this id.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(RoleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        Result<RoleResponse> result = await Sender.Send(new GetRoleByIdQuery(id), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : HandleFailure(result);
    }
}
