using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Choco.Application.Abstractions.Authentication;

namespace Choco.Infrastructure.Authentication;

internal sealed class ClerkIdentityProvider : IIdentityProvider
{
    private const string UsersPath = "users/";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private readonly HttpClient _httpClient;

    public ClerkIdentityProvider(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IdpUser?> GetUserAsync(string identityId, CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await _httpClient.GetAsync(
            UsersPath + Uri.EscapeDataString(identityId),
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        ClerkUserResponse clerkUser = (await response.Content.ReadFromJsonAsync<ClerkUserResponse>(SerializerOptions, cancellationToken))!;
        string fullName = string.Join(' ', new[] { clerkUser.FirstName, clerkUser.LastName }.Where(name => !string.IsNullOrWhiteSpace(name)));

        return new IdpUser(clerkUser.Id, clerkUser.Username, fullName.Length > 0 ? fullName : null, clerkUser.ImageUrl);
    }

    private sealed record ClerkUserResponse(string Id, string? Username, string? FirstName, string? LastName, string? ImageUrl);
}
