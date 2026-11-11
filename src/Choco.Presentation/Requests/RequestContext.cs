using System.Diagnostics;
using System.Net;
using System.Security.Claims;

using Choco.Application.Abstractions.Requests;

using Microsoft.IdentityModel.JsonWebTokens;

namespace Choco.Presentation.Requests;

internal sealed class RequestContext : IRequestContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public RequestContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? ClientId =>
        _httpContextAccessor.HttpContext?.User.FindFirstValue(JwtRegisteredClaimNames.Azp);

    public string? TraceId =>
        Activity.Current?.TraceId.ToString();

    public string? UserAgent =>
        _httpContextAccessor.HttpContext?.Request.Headers.UserAgent.ToString();

    public IPAddress? IpAddress =>
        _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress;
}
