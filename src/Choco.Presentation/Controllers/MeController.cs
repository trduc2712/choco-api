using System.Security.Claims;

using Choco.Application.Abstractions.Authentication;
using Choco.Presentation.Abstractions;
using Choco.SharedKernel.Results;

using MediatR;

using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Choco.Presentation.Controllers;

[Route("api/me")]
public sealed class MeController : ApiController
{
    private readonly ICurrentUser _currentUser;

    public MeController(ISender sender, ICurrentUser currentUser) : base(sender)
    {
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        Result<Guid> userIdResult = await _currentUser.GetIdAsync(cancellationToken);

        if (userIdResult.IsFailure)
        {
            return HandleFailure(userIdResult);
        }

        return Ok(new
        {
            id = userIdResult.Value,
            identityId = User.FindFirstValue(JwtRegisteredClaimNames.Sub),
            sessionId = User.FindFirstValue(JwtRegisteredClaimNames.Sid),
            authorizedParty = User.FindFirstValue(JwtRegisteredClaimNames.Azp)
        });
    }
}
