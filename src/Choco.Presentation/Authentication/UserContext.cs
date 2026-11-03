using System.Security.Claims;

using Choco.Application.Abstractions.Authentication;

using Microsoft.IdentityModel.JsonWebTokens;

namespace Choco.Presentation.Authentication;

internal sealed class UserContext : IUserContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public UserContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string IdentityId =>
        _httpContextAccessor.HttpContext?.User.FindFirstValue(JwtRegisteredClaimNames.Sub)
        ?? throw new InvalidOperationException("The current request has no authenticated user.");
}
