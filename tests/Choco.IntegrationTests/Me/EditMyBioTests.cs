using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using Choco.Contract.Users;
using Choco.Domain.Users;
using Choco.IntegrationTests.Authentication;

using Microsoft.AspNetCore.Mvc;

namespace Choco.IntegrationTests.Me;

public sealed class EditMyBioTests(ChocoApiFactory factory) : IClassFixture<ChocoApiFactory>, IAsyncLifetime
{
    private const string BioPath = "/api/v1/me/profile/bio";
    private const string ProfilePath = "/api/v1/me/profile";

    private readonly HttpClient _client = factory.CreateClient();

    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync()
    {
        _client.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task SavesTrimmedBioAndShowsItOnEveryProfileEndpoint()
    {
        User caller = await AddUsersAsync();
        using HttpRequestMessage request = Request(HttpMethod.Put, BioPath);
        request.Content = JsonContent.Create(new { bio = "  Bio  " });

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<UserProfileResponse>(TestContext.Current.CancellationToken))!.Bio.Should().Be("Bio");
        (await ReadAsync<UserProfileResponse>(ProfilePath)).Bio.Should().Be("Bio");
        (await ReadAsync<UserProfileDetailsResponse>($"/api/v1/users/{caller.Id}", "identity-id-2")).Bio.Should().Be("Bio");
        (await ReadAsync<UserProfileDetailsResponse>("/api/v1/users/by-username/username", "identity-id-2")).Bio.Should().Be("Bio");
        factory.IdentityProvider.Requests.Should().BeEmpty();
    }

    [Theory]
    [InlineData("""{"bio":null}""")]
    [InlineData("""{"bio":" "}""")]
    [InlineData("{}")]
    public async Task ClearsBio(string body)
    {
        await AddUsersAsync("Bio");
        using HttpRequestMessage request = Request(HttpMethod.Put, BioPath);
        request.Content = new StringContent(body, new MediaTypeHeaderValue("application/json"));

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadAsync<UserProfileResponse>(ProfilePath)).Bio.Should().BeNull();
    }

    [Fact]
    public async Task BioAtTheLimitIsAccepted()
    {
        await AddUsersAsync();
        string bio = new('b', User.BioMaxLength);
        using HttpRequestMessage request = Request(HttpMethod.Put, BioPath);
        request.Content = JsonContent.Create(new { bio });

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadAsync<UserProfileResponse>(ProfilePath)).Bio.Should().Be(bio);
    }

    [Fact]
    public async Task BioOverTheLimitReturnsValidationAndKeepsTheCurrentBio()
    {
        await AddUsersAsync("Bio");
        using HttpRequestMessage request = Request(HttpMethod.Put, BioPath);
        request.Content = JsonContent.Create(new { bio = new string('b', User.BioMaxLength + 1) });

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        ValidationProblemDetails problem = (await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(TestContext.Current.CancellationToken))!;
        problem.Errors.Keys.Should().ContainSingle().Which.Should().Be("Bio");
        ((JsonElement)problem.Extensions["code"]!).GetString().Should().Be("Validation.Failed");
        (await ReadAsync<UserProfileResponse>(ProfilePath)).Bio.Should().Be("Bio");
    }

    [Fact]
    public async Task EditingNameAndUsernameKeepsTheBio()
    {
        await AddUsersAsync("Bio");
        factory.IdentityProvider.RespondWith(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new
            {
                id = "identity-id",
                username = "username-updated",
                first_name = "First Name",
                last_name = "Last Name",
                image_url = "https://example.com/avatar.png"
            })
        });
        using HttpRequestMessage request = Request(HttpMethod.Put, ProfilePath);
        request.Content = JsonContent.Create(new { username = "username-updated", firstName = "First Name", lastName = "Last Name" });

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<UserProfileResponse>(TestContext.Current.CancellationToken))!.Bio.Should().Be("Bio");
        (await ReadAsync<UserProfileResponse>(ProfilePath)).Bio.Should().Be("Bio");
    }

    [Fact]
    public async Task ProfileWithoutBioReturnsNull()
    {
        await AddUsersAsync();

        (await ReadAsync<UserProfileResponse>(ProfilePath)).Bio.Should().BeNull();
    }

    [Fact]
    public async Task NoTokenRejectsBioWrite()
    {
        using HttpRequestMessage request = new(HttpMethod.Put, BioPath)
        {
            Content = JsonContent.Create(new { bio = "Bio" })
        };

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private async Task<User> AddUsersAsync(string? bio = null)
    {
        User caller = User.Create("identity-id", "username", "Display Name", "https://example.com/avatar.png");
        caller.ChangeBio(bio);
        User other = User.Create("identity-id-2", "username-2", "Display Name 2", null);
        await factory.ExecuteDbContextAsync(db =>
        {
            db.AddRange(caller, other);
            return db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        return caller;
    }

    private async Task<T> ReadAsync<T>(string path, string identityId = "identity-id")
    {
        using HttpRequestMessage request = Request(HttpMethod.Get, path, identityId);
        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<T>(TestContext.Current.CancellationToken)
            ?? throw new InvalidOperationException("The response was empty.");
    }

    private static HttpRequestMessage Request(HttpMethod method, string path, string identityId = "identity-id")
    {
        HttpRequestMessage request = new(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TestJwt.Create(DateTimeOffset.UtcNow, subject: identityId));
        return request;
    }
}
