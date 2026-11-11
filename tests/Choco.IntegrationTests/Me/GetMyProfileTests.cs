using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using Choco.Domain.Users;
using Choco.IntegrationTests.Authentication;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Choco.IntegrationTests.Me;

public sealed class GetMyProfileTests : IClassFixture<ChocoApiFactory>, IAsyncLifetime
{
    private readonly ChocoApiFactory _factory;
    private readonly HttpClient _client;

    public GetMyProfileTests(ChocoApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public async ValueTask InitializeAsync()
    {
        await _factory.ResetAsync();
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task NoToken_ReturnsUnauthorized()
    {
        using HttpResponseMessage response = await _client.GetAsync("/api/v1/me/profile", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("https://example.com/avatar.png")]
    public async Task ExistingUser_ReturnsOnlyBasicProfile(string? avatarUrl)
    {
        User otherUser = User.Create("identity-id-2", "username-2", "Display Name 2", "https://example.com/avatar-2.png");
        await _factory.ExecuteDbContextAsync(db =>
        {
            db.Add(otherUser);
            return db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        User target = await AddUserAsync("identity-id", "username", avatarUrl);
        using HttpRequestMessage request = Request("/api/v1/me/profile");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        body.EnumerateObject().Select(property => property.Name).Should().BeEquivalentTo("id", "username", "displayName", "avatarUrl", "bio");
        body.GetProperty("bio").ValueKind.Should().Be(JsonValueKind.Null);
        body.GetProperty("id").GetGuid().Should().Be(target.Id);
        body.GetProperty("username").GetString().Should().Be(target.Username);
        body.GetProperty("displayName").GetString().Should().Be("Display Name");
        body.GetProperty("avatarUrl").GetString().Should().Be(avatarUrl);
        _factory.IdentityProvider.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task DeletedCaller_ReturnsUnauthorized()
    {
        User caller = await AddUserAsync("identity-id", "username", null);
        await _factory.ExecuteDbContextAsync(async db =>
        {
            db.Remove(db.Set<User>().Single(user => user.Id == caller.Id));
            return await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        using HttpRequestMessage request = Request("/api/v1/me/profile");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        body.GetProperty("status").GetInt32().Should().Be(401);
        body.GetProperty("code").GetString().Should().Be("User.Deleted");
    }

    [Fact]
    public async Task NewCaller_CreatesLocalAccountAndReturnsRequestedProfile()
    {
        _factory.IdentityProvider.RespondWith(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new
            {
                id = "identity-id",
                username = "username",
                first_name = "first-name",
                last_name = "last-name",
                image_url = "https://example.com/avatar.png"
            })
        });
        using HttpRequestMessage request = Request("/api/v1/me/profile");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        body.GetProperty("username").GetString().Should().Be("username");
        body.GetProperty("displayName").GetString().Should().Be("first-name last-name");
        body.GetProperty("avatarUrl").GetString().Should().Be("https://example.com/avatar.png");
        User caller = await _factory.ExecuteDbContextAsync(db => db.Set<User>().AsNoTracking()
            .SingleAsync(user => user.IdentityId == "identity-id", TestContext.Current.CancellationToken));
        body.GetProperty("id").GetGuid().Should().Be(caller.Id);
        caller.Username.Should().Be("username");
        caller.DisplayName.Should().Be("first-name last-name");
        caller.AvatarUrl.Should().Be("https://example.com/avatar.png");
        HttpRequestMessage outgoing = _factory.IdentityProvider.Requests.Should().ContainSingle().Subject;
        outgoing.Method.Should().Be(HttpMethod.Get);
        outgoing.RequestUri.Should().Be(new Uri(ChocoApiFactory.IdentityProviderUrl, "users/identity-id"));
        outgoing.Headers.Authorization.Should().Be(new AuthenticationHeaderValue("Bearer", ChocoApiFactory.IdentityProviderSecretKey));
    }

    [Fact]
    public async Task CallerMissingFromIdentityProvider_ReturnsUnauthorizedAndCreatesNoUser()
    {
        _factory.IdentityProvider.RespondWith(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        using HttpRequestMessage request = Request("/api/v1/me/profile");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        body.GetProperty("status").GetInt32().Should().Be(401);
        body.GetProperty("code").GetString().Should().Be("User.Deleted");
        (await _factory.ExecuteDbContextAsync(db => db.Set<User>().IgnoreQueryFilters().AsNoTracking()
            .ToListAsync(TestContext.Current.CancellationToken))).Should().BeEmpty();
        _factory.IdentityProvider.Requests.Should().ContainSingle();
    }

    [Fact]
    public async Task NewCallerWithoutUsername_ReturnsValidationProblemAndCreatesNoUser()
    {
        _factory.IdentityProvider.RespondWith(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new
            {
                id = "identity-id",
                username = (string?)null,
                first_name = "first-name",
                last_name = "last-name",
                image_url = "https://example.com/avatar.png"
            })
        });
        using HttpRequestMessage request = Request("/api/v1/me/profile");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        ValidationProblemDetails body = (await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(TestContext.Current.CancellationToken))!;
        body.Status.Should().Be(400);
        body.Errors.Keys.Should().ContainSingle().Which.Should().Be("Username");
        body.Extensions["code"].Should().BeOfType<JsonElement>().Which.GetString().Should().Be("Validation.Failed");
        (await _factory.ExecuteDbContextAsync(db => db.Set<User>().IgnoreQueryFilters().AsNoTracking()
            .ToListAsync(TestContext.Current.CancellationToken))).Should().BeEmpty();
        _factory.IdentityProvider.Requests.Should().ContainSingle();
    }

    private async Task<User> AddUserAsync(string identityId, string username, string? avatarUrl)
    {
        User user = User.Create(identityId, username, "Display Name", avatarUrl);
        await _factory.ExecuteDbContextAsync(db =>
        {
            db.Add(user);
            return db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        return user;
    }

    private static HttpRequestMessage Request(string path)
    {
        HttpRequestMessage request = new(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TestJwt.Create(DateTimeOffset.UtcNow));
        return request;
    }
}
