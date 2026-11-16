using Asp.Versioning;

using Choco.Contract.Roles;
using Choco.Presentation.Abstractions;
using Choco.SharedKernel.Results;

using MediatR;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

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
    /// <response code="200">Every role with its identity, appearance, permissions, holder summary and last change.</response>
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

    /// <summary>Creates a role.</summary>
    /// <remarks>Only a caller with the permission to create roles may call it, and only with permissions the caller holds. The new role is not the default role and has no system key. Leading and trailing whitespace is removed from the name. Sending the same body twice fails the second time, because role names are unique ignoring case. The new role has no holders until it is assigned to a user.</remarks>
    /// <param name="command">The role's name, appearance and the permissions it grants.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <response code="201">The role was created; the Location header points to it.</response>
    /// <response code="400">A required field is missing or invalid, the description is too long, or a permission is outside the catalog.</response>
    /// <response code="401">The session is invalid or the caller's account is deleted.</response>
    /// <response code="403">The caller may not create roles, or lists a permission the caller does not hold. It is also returned when the caller's account is suspended.</response>
    /// <response code="409">Another role already has this name, ignoring case.</response>
    [HttpPost]
    [ProducesResponseType(typeof(RoleResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateRoleCommand command, CancellationToken cancellationToken)
    {
        Result<RoleResponse> result = await Sender.Send(command, cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { version = RouteData.Values["version"], id = result.Value.Id }, result.Value)
            : HandleFailure(result);
    }

    /// <summary>Updates a role's name, appearance and permissions.</summary>
    /// <remarks>Only a caller with the permission to update roles may call it. It replaces the role's name, appearance and entire permission set. An optional reason is recorded in the audit log. System roles and the default role can be changed too; their key stays. The caller must hold every permission the role has now and every permission in the body, and cannot change a role assigned to the caller. The change applies to the role's holders on their next request. Leading and trailing whitespace is removed from the name.</remarks>
    /// <param name="id" example="00000000-0000-0000-0000-000000000001">The role's id.</param>
    /// <param name="command">The updated role name, appearance and permissions, with an optional audit reason.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <response code="200">The updated role.</response>
    /// <response code="400">The id is empty, a role field is invalid, a permission is outside the catalog, or the audit reason is too long.</response>
    /// <response code="401">The session is invalid or the caller's account is deleted.</response>
    /// <response code="403">The caller may not update roles, holds the role, or lacks a permission of the role or of the body. It is also returned when the caller's account is suspended.</response>
    /// <response code="404">No role has this id.</response>
    /// <response code="409">Another role already has this name, ignoring case.</response>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(RoleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateRoleCommand command, CancellationToken cancellationToken)
    {
        Result<RoleResponse> result = await Sender.Send(command with { Id = id }, cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : HandleFailure(result);
    }

    /// <summary>Previews how replacing a role's permissions would affect its holders.</summary>
    /// <remarks>Requires the permission to update roles. The caller cannot hold this role and must hold its current and proposed permissions. The preview leaves the role unchanged.</remarks>
    /// <param name="id" example="00000000-0000-0000-0000-000000000001">The role's id.</param>
    /// <param name="permissions" example="reports.handle">Repeat this query parameter for each proposed permission. Omit it to preview removing every permission.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <response code="200">The affected holder count and the added and removed permissions.</response>
    /// <response code="400">The id is empty or a permission is not in the catalog.</response>
    /// <response code="401">The session is invalid or the caller's account is deleted.</response>
    /// <response code="403">The caller may not update this role or lacks a current or proposed permission.</response>
    /// <response code="404">No role has this id.</response>
    [HttpGet("{id:guid}/impact")]
    [ProducesResponseType(typeof(RoleImpactResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetImpact(Guid id, [FromQuery] string[]? permissions, CancellationToken cancellationToken)
    {
        Result<RoleImpactResponse> result = await Sender.Send(new GetRoleImpactQuery(id, permissions ?? []), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : HandleFailure(result);
    }

    /// <summary>Deletes a role.</summary>
    /// <remarks>Only a caller with the permission to delete roles may call it. Everyone who holds the role loses it. If the optional body names a replacement role, its holders receive that role in the same transaction. The caller also needs the permission to assign roles and must hold every permission granted by the replacement. The default role and roles the system created cannot be deleted. The caller must hold every permission of the role and cannot delete a role assigned to the caller. A deleted role is no longer found, so deleting it again returns 404.</remarks>
    /// <param name="id" example="00000000-0000-0000-0000-000000000001">The role's id.</param>
    /// <param name="command">Optional replacement role and audit reason.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <response code="204">The role was deleted.</response>
    /// <response code="400">The id is empty, the audit reason is too long, or the replacement is the deleted role or the default role.</response>
    /// <response code="401">The session is invalid or the caller's account is deleted.</response>
    /// <response code="403">The caller may not delete roles, holds the role, or lacks a permission of the role. It is also returned when the caller's account is suspended.</response>
    /// <response code="404">The role to delete or the replacement does not exist.</response>
    /// <response code="409">The role is the default role or was created by the system.</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(
        Guid id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] DeleteRoleCommand? command,
        CancellationToken cancellationToken)
    {
        Result<RoleResponse> result = await Sender.Send((command ?? new DeleteRoleCommand(id)) with { Id = id }, cancellationToken);

        return result.IsSuccess ? NoContent() : HandleFailure(result);
    }

    /// <summary>Gets one role with the permissions it holds.</summary>
    /// <remarks>Only a caller with the permission to read roles may call it. The role's permissions are in alphabetical order.</remarks>
    /// <param name="id" example="00000000-0000-0000-0000-000000000001">The role's id.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <response code="200">The role with its identity, appearance, permissions, holder summary and last change.</response>
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
