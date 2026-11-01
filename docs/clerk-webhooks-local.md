# Clerk Webhooks on a Local Machine

Clerk sends webhooks to a public URL and cannot reach `localhost`. A [Dev Tunnel](https://learn.microsoft.com/azure/developer/dev-tunnels/) gives your machine a public URL and forwards requests to the API.

Every developer has their own tunnel and their own Clerk webhook endpoint.

## Prerequisites

- Access to the Clerk development instance.
- A Microsoft or GitHub account.
- The `devtunnel` CLI:

| OS | Install |
| --- | --- |
| Windows | `winget install Microsoft.devtunnel` |
| macOS | `brew install --cask devtunnel` |
| Linux | `curl -sL https://aka.ms/DevTunnelCliInstall \| bash` |

## One-Time Setup

1. Sign in. Add `-g` to use GitHub.

   ```bash
   devtunnel user login
   ```

2. Create your tunnel and open the API's HTTPS port on it. Replace `<name>` with your name.

   ```bash
   devtunnel create choco-api-<name> --allow-anonymous
   devtunnel port create choco-api-<name> -p 7000 --protocol https
   ```

   Anonymous access is required because Clerk cannot sign in to the tunnel. The webhook signature protects the endpoint.

3. Host the tunnel once and note the `Connect via browser` URL, such as `https://<id>-7000.<region>.devtunnels.ms`.

   ```bash
   devtunnel host choco-api-<name>
   ```

4. In the Clerk Dashboard of the development instance, open **Configure → Webhooks** and add an endpoint:
   - URL: `<tunnel-url>/api/webhooks/clerk`
   - Events: all `user.*` events

5. Copy the endpoint's signing secret and store it in user secrets:

   ```bash
   dotnet user-secrets set Clerk:WebhookSigningSecret "<whsec_...>" --project Choco.Presentation
   ```

## Each Session

1. Start the tunnel and keep its terminal open:

   ```bash
   devtunnel host choco-api-<name>
   ```

2. Run the API with the `https` profile: `./build.ps1 Run`, or select `https` in Visual Studio. The tunnel forwards to port 7000; on port 5000 `UseHttpsRedirection` would redirect Clerk to `localhost`.

Stop the tunnel when you no longer need webhooks.

## Check

- **Users → Create user** with a username: the API logs `StatusCode: 200` and a row appears in `users`.
- **Send example** on the endpoint's Testing tab returns 400: the example payload has `"username": null`, which the validator rejects.

## Troubleshooting

| Symptom | Cause |
| --- | --- |
| 502 | The API is not running, or the tunnel is not hosted. |
| 307 | The API runs with the `http` profile, or the tunnel forwards to port 5000. |
| 401 | The signing secret in user secrets is not the secret of your endpoint. |
| 200 in Clerk but no log line and no row | The request did not reach your API. Check the endpoint URL and the tunnel's inspect page (`https://<id>-7000-inspect.<region>.devtunnels.ms`). |
| The tunnel no longer exists | A tunnel expires after 30 days without hosting. Repeat the one-time setup and update the endpoint URL. |

## Notes

- Every endpoint of the development instance receives every event, so your database also receives users created by other developers.
- Clerk retries a delivery that did not return 2xx for several days. Replay a message from the endpoint's **Message Attempts** once the tunnel and the API are running.
- Production does not use a tunnel; its endpoint points at the deployed API.
