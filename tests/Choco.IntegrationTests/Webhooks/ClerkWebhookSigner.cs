using System.Globalization;
using System.Text;

using Svix;

namespace Choco.IntegrationTests.Webhooks;

internal static class ClerkWebhookSigner
{
    public static readonly string SigningSecret =
        "whsec_" + Convert.ToBase64String("webhook-signing-secret"u8);

    public static HttpRequestMessage CreateSignedRequest(string payload, string messageId, DateTimeOffset timestamp)
    {
        Webhook webhook = new(SigningSecret);

        HttpRequestMessage request = new(HttpMethod.Post, "/api/v1/webhooks/clerk")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("svix-id", messageId);
        request.Headers.Add("svix-timestamp", timestamp.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture));
        request.Headers.Add("svix-signature", webhook.Sign(messageId, timestamp, payload));

        return request;
    }
}
