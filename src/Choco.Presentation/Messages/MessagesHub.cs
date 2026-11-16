using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Choco.Presentation.Messages;

[Authorize]
public sealed class MessagesHub : Hub
{
    public const string Path = "/hubs/messages";
    public const string MessageReceivedEvent = "messageReceived";
}
