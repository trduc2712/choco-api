using System.Text.Json;

using Asp.Versioning;

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

/// <summary>
/// Signed Clerk events that synchronize local users.
/// </summary>
[Route("api/v{version:apiVersion}/webhooks/clerk")]
[ApiVersion(1.0)]
[AllowAnonymous]
public sealed class ClerkWebhooksController : ApiController
{
    private const string UserCreatedEventType = "user.created";
    private const string UserUpdatedEventType = "user.updated";
    private const string UserDeletedEventType = "user.deleted";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private readonly Webhook _webhook;

    public ClerkWebhooksController(ISender sender, Webhook webhook) : base(sender)
    {
        _webhook = webhook;
    }

    /// <summary>Receives a Clerk webhook.</summary>
    /// <remarks>
    /// Requires a valid webhook signature in the svix-id, svix-timestamp and svix-signature headers; no bearer token is needed.
    /// A user.created event creates the local user. A user.updated event changes the local user's username, display name and avatar to match Clerk, unless an event with the same or a later Clerk update time was already applied, so a repeated or late event changes nothing. A user.deleted event soft-deletes the local user, which frees their username.
    /// Repeated events, events for a user the API does not know and other event types are acknowledged without a change.
    /// </remarks>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <response code="200">The event was processed or acknowledged.</response>
    /// <response code="400">The user details are invalid; errors lists the field.</response>
    /// <response code="401">The webhook signature or timestamp is missing or invalid.</response>
    /// <response code="409">A user.updated event gives a username that another user holds. Clerk retries the event, which succeeds once the other user's event has been applied.</response>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
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
            UserUpdatedEventType => await SyncUserProfileAsync(webhookEvent.Data, cancellationToken),
            UserDeletedEventType => await SyncUserDeletionAsync(webhookEvent.Data, cancellationToken),
            _ => Ok()
        };
    }

    private async Task<IActionResult> SyncUserProfileAsync(JsonElement data, CancellationToken cancellationToken)
    {
        ClerkUserData userData = data.Deserialize<ClerkUserData>(SerializerOptions)!;

        Result result = await Sender.Send(userData.ToSyncUserProfileCommand(), cancellationToken);

        return result.IsSuccess ? Ok() : HandleFailure(result);
    }

    private async Task<IActionResult> SyncUserDeletionAsync(JsonElement data, CancellationToken cancellationToken)
    {
        ClerkDeletedUserData userData = data.Deserialize<ClerkDeletedUserData>(SerializerOptions)!;

        Result result = await Sender.Send(new SyncUserDeletionCommand(userData.Id), cancellationToken);

        return result.IsSuccess ? Ok() : HandleFailure(result);
    }

    private async Task<IActionResult> CreateUserAsync(JsonElement data, CancellationToken cancellationToken)
    {
        ClerkUserData userData = data.Deserialize<ClerkUserData>(SerializerOptions)!;
        CreateUserCommand command = userData.ToCreateUserCommand();

        Result result = await Sender.Send(command, cancellationToken);

        return result.IsSuccess ? Ok() : HandleFailure(result);
    }
}
