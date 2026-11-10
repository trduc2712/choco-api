using System.Security.Claims;

using Microsoft.AspNetCore.SignalR;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Choco.Presentation.Notifications;

internal sealed class IdentityUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection)
    {
        return connection.User.FindFirstValue(JwtRegisteredClaimNames.Sub);
    }
}
