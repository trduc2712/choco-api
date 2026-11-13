using System.Collections;
using System.Security.Claims;

using Microsoft.IdentityModel.JsonWebTokens;

namespace Choco.Presentation.Logging;

internal sealed class UserLogScopeMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<UserLogScopeMiddleware> _logger;

    public UserLogScopeMiddleware(RequestDelegate next, ILogger<UserLogScopeMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        using IDisposable? scope = _logger.BeginScope(new UserLogScope(context));

        await _next(context);
    }

    private sealed class UserLogScope : IEnumerable<KeyValuePair<string, object>>
    {
        public const string IdentityIdKey = "IdentityId";

        private readonly HttpContext _context;

        public UserLogScope(HttpContext context)
        {
            _context = context;
        }

        public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
        {
            string? identityId = _context.User.FindFirstValue(JwtRegisteredClaimNames.Sub);

            if (identityId is not null)
            {
                yield return new KeyValuePair<string, object>(IdentityIdKey, identityId);
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public override string ToString()
        {
            return "User";
        }
    }
}
