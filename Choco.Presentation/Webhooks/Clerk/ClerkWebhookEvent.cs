using System.Text.Json;

namespace Choco.Presentation.Webhooks.Clerk;

internal sealed record ClerkWebhookEvent(string Type, JsonElement Data);
