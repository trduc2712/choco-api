using System.Text.Json;

using Choco.Contract.Users;
using Choco.Presentation.Abstractions;
using Choco.Presentation.Webhooks.Clerk;
using Choco.SharedKernel.Results;

using MediatR;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Svix;
using Svix.Exceptions;

namespace Choco.Presentation.Controllers;

[Route("api/webhooks/clerk")]
[AllowAnonymous]
public sealed class ClerkWebhooksController : ApiController
{
    private const string UserCreatedEventType = "user.created";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private readonly Webhook _webhook;

    public ClerkWebhooksController(ISender sender, Webhook webhook) : base(sender)
    {
        _webhook = webhook;
    }

    [HttpPost]
    public async Task<IActionResult> Receive(CancellationToken cancellationToken)
    {
        using StreamReader reader = new(Request.Body);
        string payload = await reader.ReadToEndAsync(cancellationToken);

        try
        {
            _webhook.Verify(payload, headerName => Request.Headers[headerName!].ToString());
        }
        catch (WebhookVerificationException)
        {
            return Unauthorized();
        }

        ClerkWebhookEvent webhookEvent = JsonSerializer.Deserialize<ClerkWebhookEvent>(payload, SerializerOptions)!;

        return webhookEvent.Type switch
        {
            UserCreatedEventType => await CreateUserAsync(webhookEvent.Data, cancellationToken),
            _ => Ok()
        };
    }

    private async Task<IActionResult> CreateUserAsync(JsonElement data, CancellationToken cancellationToken)
    {
        ClerkUserData userData = data.Deserialize<ClerkUserData>(SerializerOptions)!;
        CreateUserCommand command = userData.ToCreateUserCommand();

        Result result = await Sender.Send(command, cancellationToken);

        return result.IsSuccess ? Ok() : HandleFailure(result);
    }
}
