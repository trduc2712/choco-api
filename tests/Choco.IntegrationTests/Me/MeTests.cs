using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;

using Choco.Domain.Roles;
using Choco.Domain.Users;
using Choco.IntegrationTests.Authentication;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace Choco.IntegrationTests.Me;

public sealed class MeTests : IClassFixture<ChocoApiFactory>, IAsyncLifetime
{
    private readonly ChocoApiFactory _factory;
    private readonly HttpClient _client;

    public MeTests(ChocoApiFactory factory)
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
        using HttpRequestMessage request = new(HttpMethod.Get, "/api/v1/me");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task TokenSignedByUnknownKey_ReturnsUnauthorized()
    {
        RsaSecurityKey unknownKey = new(RSA.Create(2048)) { KeyId = "unknown-signing-key-id" };
        using HttpRequestMessage request = CreateRequest(TestJwt.Create(DateTimeOffset.UtcNow, signingKey: unknownKey));

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ExpiredToken_ReturnsUnauthorized()
    {
        using HttpRequestMessage request = CreateRequest(TestJwt.Create(DateTimeOffset.UtcNow.AddMinutes(-10)));

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task TokenFromUnknownIssuer_ReturnsUnauthorized()
    {
        using HttpRequestMessage request = CreateRequest(TestJwt.Create(DateTimeOffset.UtcNow, issuer: "https://issuer.example.org"));

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task TokenForUnauthorizedParty_ReturnsUnauthorized()
    {
        using HttpRequestMessage request = CreateRequest(TestJwt.Create(DateTimeOffset.UtcNow, authorizedParty: "https://party.example.org"));

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task TokenWithoutAuthorizedParty_ReturnsUnauthorized()
    {
        using HttpRequestMessage request = CreateRequest(TestJwt.Create(DateTimeOffset.UtcNow, authorizedParty: null));

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ExistingUser_ReturnsUserWithoutCallingIdentityProvider()
    {
        User user = User.Create("identity-id", "username", "Display Name", "https://example.com/avatar.png");
        await _factory.ExecuteDbContextAsync(dbContext =>
        {
            dbContext.Add(user);
            return dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        using HttpRequestMessage request = CreateRequest(TestJwt.Create(DateTimeOffset.UtcNow));

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        MeResponse body = (await response.Content.ReadFromJsonAsync<MeResponse>(TestContext.Current.CancellationToken))!;
        body.Should().BeEquivalentTo(new MeResponse(user.Id, "identity-id", "session-id", TestJwt.AuthorizedParty, []));
        _factory.IdentityProvider.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task Administrator_ReturnsSortedAdministratorPermissions()
    {
        Guid adminRoleId = await _factory.ExecuteDbContextAsync(dbContext => dbContext.Set<Role>()
            .Where(role => role.SystemKey == SystemRoles.Admin)
            .Select(role => role.Id)
            .SingleAsync(TestContext.Current.CancellationToken));
        User user = User.Create("identity-id", "username", "Display Name", null);
        user.AssignRole(adminRoleId, null, ChocoApiFactory.StartTime);
        await _factory.ExecuteDbContextAsync(dbContext =>
        {
            dbContext.Add(user);
            return dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        using HttpRequestMessage request = CreateRequest(TestJwt.Create(DateTimeOffset.UtcNow));

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        string[] permissions = body.GetProperty("permissions").EnumerateArray().Select(item => item.GetString()!).ToArray();
        permissions.Should().Equal(SystemRoles.All.Single(role => role.Key == SystemRoles.Admin)
            .Permissions.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Member_ReturnsEmptyPermissions()
    {
        User user = User.Create("identity-id", "username", "Display Name", null);
        await _factory.ExecuteDbContextAsync(dbContext =>
        {
            dbContext.Add(user);
            return dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        using HttpRequestMessage request = CreateRequest(TestJwt.Create(DateTimeOffset.UtcNow));

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        body.EnumerateObject().Select(property => property.Name)
            .Should().BeEquivalentTo("id", "identityId", "sessionId", "authorizedParty", "permissions", "suspension");
        body.GetProperty("permissions").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task NewUser_CreatesUserFromIdentityProvider()
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
        using HttpRequestMessage request = CreateRequest(TestJwt.Create(DateTimeOffset.UtcNow));

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        MeResponse body = (await response.Content.ReadFromJsonAsync<MeResponse>(TestContext.Current.CancellationToken))!;
        User user = (await GetUsersAsync()).Should().ContainSingle().Subject;
        body.Should().BeEquivalentTo(new MeResponse(user.Id, "identity-id", "session-id", TestJwt.AuthorizedParty, []));
        user.IdentityId.Should().Be("identity-id");
        user.Username.Should().Be("username");
        user.DisplayName.Should().Be("first-name last-name");
        user.AvatarUrl.Should().Be("https://example.com/avatar.png");
        user.CreatedAt.Should().Be(ChocoApiFactory.StartTime);
        HttpRequestMessage identityProviderRequest = _factory.IdentityProvider.Requests.Should().ContainSingle().Subject;
        identityProviderRequest.Method.Should().Be(HttpMethod.Get);
        identityProviderRequest.RequestUri.Should().Be(new Uri(ChocoApiFactory.IdentityProviderUrl, "users/identity-id"));
        identityProviderRequest.Headers.Authorization.Should().Be(
            new AuthenticationHeaderValue("Bearer", ChocoApiFactory.IdentityProviderSecretKey));
    }

    [Fact]
    public async Task DeletedUser_ReturnsUnauthorized()
    {
        User user = User.Create("identity-id", "username", "Display Name", null);
        await _factory.ExecuteDbContextAsync(async dbContext =>
        {
            dbContext.Add(user);
            await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
            dbContext.Remove(user);
            return await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        using HttpRequestMessage request = CreateRequest(TestJwt.Create(DateTimeOffset.UtcNow));

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await ShouldBeUserDeletedProblemAsync(response);
        _factory.IdentityProvider.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task UserUnknownToIdentityProvider_ReturnsUnauthorizedAndCreatesNoUser()
    {
        _factory.IdentityProvider.RespondWith(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        using HttpRequestMessage request = CreateRequest(TestJwt.Create(DateTimeOffset.UtcNow));

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await ShouldBeUserDeletedProblemAsync(response);
        (await GetUsersAsync()).Should().BeEmpty();
        _factory.IdentityProvider.Requests.Should().ContainSingle();
    }

    private static HttpRequestMessage CreateRequest(string token)
    {
        HttpRequestMessage request = new(HttpMethod.Get, "/api/v1/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return request;
    }

    private static async Task ShouldBeUserDeletedProblemAsync(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        ProblemDetails problem = (await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken))!;
        problem.Status.Should().Be(StatusCodes.Status401Unauthorized);
        problem.Extensions["code"].Should().BeOfType<JsonElement>().Which.GetString().Should().Be("User.Deleted");
    }

    private Task<List<User>> GetUsersAsync()
    {
        return _factory.ExecuteDbContextAsync(dbContext => dbContext.Set<User>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .ToListAsync(TestContext.Current.CancellationToken));
    }

    private sealed record MeResponse(Guid Id, string IdentityId, string SessionId, string AuthorizedParty, string[] Permissions);
}
