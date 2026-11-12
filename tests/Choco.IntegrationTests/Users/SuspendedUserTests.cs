using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using Choco.Domain.Roles;
using Choco.Domain.Users;
using Choco.IntegrationTests.Authentication;

using Microsoft.EntityFrameworkCore;

namespace Choco.IntegrationTests.Users;

public sealed class SuspendedUserTests : IClassFixture<ChocoApiFactory>, IAsyncLifetime
{
    private const string Id = "00000000-0000-0000-0000-000000000001";
    private const string Feedback = """{"category":"bug","message":"message","context":{"path":"/path","locale":"en","theme":"system","userAgent":"user-agent","viewport":{"width":1280,"height":720},"appVersion":"app-version"}}""";

    private readonly ChocoApiFactory _factory;
    private readonly HttpClient _client;

    public SuspendedUserTests(ChocoApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public static TheoryData<string, string, string?> WriteRequests => new()
    {
        { "POST", "/api/v1/posts", """{"content":"content"}""" },
        { "PUT", $"/api/v1/posts/{Id}", """{"content":"content"}""" },
        { "DELETE", $"/api/v1/posts/{Id}", null },
        { "PUT", $"/api/v1/posts/{Id}/like", null },
        { "DELETE", $"/api/v1/posts/{Id}/like", null },
        { "POST", $"/api/v1/posts/{Id}/comments", """{"content":"content"}""" },
        { "POST", $"/api/v1/posts/{Id}/reports", """{"reason":"Spam","details":"details"}""" },
        { "DELETE", $"/api/v1/comments/{Id}", null },
        { "PUT", $"/api/v1/users/{Id}/follow", null },
        { "DELETE", $"/api/v1/users/{Id}/follow", null },
        { "PUT", $"/api/v1/users/{Id}/block", null },
        { "DELETE", $"/api/v1/users/{Id}/block", null },
        { "POST", $"/api/v1/users/{Id}/reports", """{"reason":"Harassment","details":"details"}""" },
        { "PUT", "/api/v1/me/profile", """{"username":"username","firstName":"First Name"}""" },
        { "PUT", "/api/v1/me/profile/bio", """{"bio":"bio"}""" },
        { "DELETE", "/api/v1/me/profile/avatar", null },
        { "PUT", $"/api/v1/me/notifications/{Id}/read", null },
        { "PUT", "/api/v1/me/notifications/read", null },
        { "POST", "/api/v1/feedback", Feedback }
    };

    public async ValueTask InitializeAsync()
    {
        await _factory.ResetAsync();
    }

    public ValueTask DisposeAsync()
    {
        _client.Dispose();

        return ValueTask.CompletedTask;
    }

    [Theory]
    [MemberData(nameof(WriteRequests))]
    public async Task WriteRequest_OfASuspendedUser_IsRefusedWithTheReasonAndTheTime(string method, string path, string? body)
    {
        await AddSuspendedUserAsync("identity-id", "reason");

        using HttpResponseMessage response = await SendAsync("identity-id", new HttpMethod(method), path, body);

        await ShouldBeSuspendedAsync(response, "reason");
    }

    [Fact]
    public async Task InvalidBody_OfASuspendedUser_IsRefusedBeforeItIsValidated()
    {
        await AddSuspendedUserAsync("identity-id", "reason");

        using HttpResponseMessage response = await SendAsync("identity-id", HttpMethod.Post, "/api/v1/posts", """{"content":""}""");

        await ShouldBeSuspendedAsync(response, "reason");
    }

    [Fact]
    public async Task AdministratorCommand_OfASuspendedMember_IsRefusedByThePermissionCheckFirst()
    {
        await AddSuspendedUserAsync("identity-id", "reason");

        using HttpResponseMessage response = await SendAsync(
            "identity-id", HttpMethod.Put, $"/api/v1/admin/users/{Id}/suspension", """{"reason":"reason"}""");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        body.GetProperty("code").GetString().Should().Be("Permission.Denied");
    }

    [Fact]
    public async Task AdministratorCommand_OfASuspendedAdministrator_IsRefused()
    {
        User administrator = await AddSuspendedUserAsync("identity-id", "reason", await GetRoleIdAsync(SystemRoles.Admin));
        User other = await AddUserAsync("identity-id-2", "username-2");

        using HttpResponseMessage response = await SendAsync(
            "identity-id", HttpMethod.Put, $"/api/v1/admin/users/{other.Id}/suspension", """{"reason":"reason"}""");

        await ShouldBeSuspendedAsync(response, "reason");
        (await GetUserAsync(other.Id)).IsSuspended.Should().BeFalse();
        administrator.IsSuspended.Should().BeTrue();
    }

    [Fact]
    public async Task Reads_OfASuspendedUser_StillWork()
    {
        await AddSuspendedUserAsync("identity-id", "reason", await GetRoleIdAsync(SystemRoles.Admin));

        using HttpResponseMessage me = await SendAsync("identity-id", HttpMethod.Get, "/api/v1/me", null);
        using HttpResponseMessage posts = await SendAsync("identity-id", HttpMethod.Get, "/api/v1/me/posts", null);
        using HttpResponseMessage users = await SendAsync("identity-id", HttpMethod.Get, "/api/v1/admin/users", null);

        me.StatusCode.Should().Be(HttpStatusCode.OK);
        posts.StatusCode.Should().Be(HttpStatusCode.OK);
        users.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Me_OfASuspendedUser_CarriesTheReasonAndTheTime()
    {
        User user = await AddSuspendedUserAsync("identity-id", "reason");

        using HttpResponseMessage response = await SendAsync("identity-id", HttpMethod.Get, "/api/v1/me", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        body.GetProperty("id").GetGuid().Should().Be(user.Id);
        JsonElement suspension = body.GetProperty("suspension");
        suspension.GetProperty("reason").GetString().Should().Be("reason");
        suspension.GetProperty("suspendedAt").GetDateTimeOffset().Should().Be(ChocoApiFactory.StartTime);
    }

    [Fact]
    public async Task Me_OfAUserWhoIsNotSuspended_HasNoSuspension()
    {
        await AddUserAsync("identity-id", "username");

        using HttpResponseMessage response = await SendAsync("identity-id", HttpMethod.Get, "/api/v1/me", null);

        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        body.GetProperty("suspension").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task UserWhoIsNotSuspended_CanStillWrite()
    {
        await AddUserAsync("identity-id", "username");

        using HttpResponseMessage response = await SendAsync("identity-id", HttpMethod.Post, "/api/v1/posts", """{"content":"content"}""");

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task SuspendingThroughTheApi_RefusesTheNextRequestAndLiftingAllowsItAgain()
    {
        await AddUserAsync("identity-id", "username");
        User administrator = await AddUserAsync("identity-id-2", "username-2", await GetRoleIdAsync(SystemRoles.Admin));
        User member = await GetUserByIdentityAsync("identity-id");

        using HttpResponseMessage before = await SendAsync("identity-id", HttpMethod.Post, "/api/v1/posts", """{"content":"content"}""");
        using HttpResponseMessage suspend = await SendAsync(
            "identity-id-2", HttpMethod.Put, $"/api/v1/admin/users/{member.Id}/suspension", """{"reason":"Repeated spam"}""");
        using HttpResponseMessage whileSuspended = await SendAsync("identity-id", HttpMethod.Post, "/api/v1/posts", """{"content":"content"}""");
        using HttpResponseMessage lift = await SendAsync(
            "identity-id-2", HttpMethod.Delete, $"/api/v1/admin/users/{member.Id}/suspension", null);
        using HttpResponseMessage after = await SendAsync("identity-id", HttpMethod.Post, "/api/v1/posts", """{"content":"content"}""");

        before.StatusCode.Should().Be(HttpStatusCode.Created);
        suspend.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await ShouldBeSuspendedAsync(whileSuspended, "Repeated spam");
        lift.StatusCode.Should().Be(HttpStatusCode.NoContent);
        after.StatusCode.Should().Be(HttpStatusCode.Created);
        administrator.Id.Should().NotBe(member.Id);
    }

    private static async Task ShouldBeSuspendedAsync(HttpResponseMessage response, string reason)
    {
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        body.GetProperty("code").GetString().Should().Be("User.Suspended");
        body.GetProperty("detail").GetString().Should().Contain(reason);
        JsonElement suspension = body.GetProperty("suspension");
        suspension.GetProperty("reason").GetString().Should().Be(reason);
        suspension.GetProperty("suspendedAt").ValueKind.Should().Be(JsonValueKind.String);
    }

    private async Task<HttpResponseMessage> SendAsync(string identityId, HttpMethod method, string path, string? body)
    {
        using HttpRequestMessage request = new(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer", TestJwt.Create(DateTimeOffset.UtcNow, subject: identityId));

        if (body is not null)
        {
            request.Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
        }

        return await _client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private async Task<User> AddUserAsync(string identityId, string username, Guid? roleId = null)
    {
        User user = User.Create(identityId, username, "Display Name", null);

        if (roleId is not null)
        {
            user.AssignRole(roleId.Value, null, ChocoApiFactory.StartTime);
        }

        await _factory.ExecuteDbContextAsync(dbContext =>
        {
            dbContext.Add(user);
            return dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        return user;
    }

    private async Task<User> AddSuspendedUserAsync(string identityId, string reason, Guid? roleId = null)
    {
        User user = User.Create(identityId, "username", "Display Name", null);
        user.Suspend(reason, user.Id, ChocoApiFactory.StartTime);

        if (roleId is not null)
        {
            user.AssignRole(roleId.Value, null, ChocoApiFactory.StartTime);
        }

        await _factory.ExecuteDbContextAsync(dbContext =>
        {
            dbContext.Add(user);
            return dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        return user;
    }

    private Task<User> GetUserAsync(Guid id)
    {
        return _factory.ExecuteDbContextAsync(dbContext => dbContext.Set<User>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync(user => user.Id == id, TestContext.Current.CancellationToken));
    }

    private Task<User> GetUserByIdentityAsync(string identityId)
    {
        return _factory.ExecuteDbContextAsync(dbContext => dbContext.Set<User>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync(user => user.IdentityId == identityId, TestContext.Current.CancellationToken));
    }

    private Task<Guid> GetRoleIdAsync(string systemKey)
    {
        return _factory.ExecuteDbContextAsync(dbContext => dbContext.Set<Role>()
            .Where(role => role.SystemKey == systemKey)
            .Select(role => role.Id)
            .SingleAsync(TestContext.Current.CancellationToken));
    }
}
