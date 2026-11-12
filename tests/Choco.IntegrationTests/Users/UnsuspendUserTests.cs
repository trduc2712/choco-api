using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using Choco.Domain.Roles;
using Choco.Domain.Users;
using Choco.IntegrationTests.Authentication;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Choco.IntegrationTests.Users;

public sealed class UnsuspendUserTests : IClassFixture<ChocoApiFactory>, IAsyncLifetime
{
    private static readonly Guid UnknownId = new("00000000-0000-0000-0000-000000000001");

    private readonly ChocoApiFactory _factory;
    private readonly HttpClient _client;

    public UnsuspendUserTests(ChocoApiFactory factory)
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
        _client.Dispose();

        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task NoToken_ReturnsUnauthorized()
    {
        using HttpRequestMessage request = new(HttpMethod.Delete, PathOf(UnknownId));

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Member_ReturnsForbiddenAndKeepsTheSuspension()
    {
        await AddUserAsync("identity-id", "username");
        User target = await AddSuspendedUserAsync();

        using HttpResponseMessage response = await SendAsync(target.Id);

        await ShouldBeProblemAsync(response, HttpStatusCode.Forbidden, "Permission.Denied");
        (await GetUserAsync(target.Id)).IsSuspended.Should().BeTrue();
    }

    [Fact]
    public async Task UserWithAnotherPermissionButNotSuspendUsers_ReturnsForbidden()
    {
        await AddCallerAsync(Permission.ReadAnyUser, Permission.AssignRoles);
        User target = await AddSuspendedUserAsync();

        using HttpResponseMessage response = await SendAsync(target.Id);

        await ShouldBeProblemAsync(response, HttpStatusCode.Forbidden, "Permission.Denied");
        (await GetUserAsync(target.Id)).IsSuspended.Should().BeTrue();
    }

    [Fact]
    public async Task EmptyUserId_ReturnsBadRequest()
    {
        await AddCallerAsync(Permission.SuspendUsers);

        using HttpResponseMessage response = await SendAsync(Guid.Empty);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UnknownUser_ReturnsNotFound()
    {
        await AddCallerAsync(Permission.SuspendUsers);

        using HttpResponseMessage response = await SendAsync(UnknownId);

        await ShouldBeProblemAsync(response, HttpStatusCode.NotFound, "User.NotFound");
    }

    [Fact]
    public async Task DeletedUser_ReturnsNotFound()
    {
        await AddCallerAsync(Permission.SuspendUsers);
        User target = await AddSuspendedUserAsync();
        await DeleteUserAsync(target.Id);

        using HttpResponseMessage response = await SendAsync(target.Id);

        await ShouldBeProblemAsync(response, HttpStatusCode.NotFound, "User.NotFound");
    }

    [Fact]
    public async Task SuspendedUser_IsNoLongerSuspended()
    {
        await AddCallerAsync(Permission.SuspendUsers);
        User target = await AddSuspendedUserAsync();

        using HttpResponseMessage response = await SendAsync(target.Id);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        User stored = await GetUserAsync(target.Id);
        stored.IsSuspended.Should().BeFalse();
        stored.SuspendedAt.Should().BeNull();
        stored.SuspendedBy.Should().BeNull();
        stored.SuspensionReason.Should().BeNull();
    }

    [Fact]
    public async Task UserWhoIsNotSuspended_ReturnsNoContent()
    {
        await AddCallerAsync(Permission.SuspendUsers);
        User target = await AddUserAsync("identity-id-2", "username-2");

        using HttpResponseMessage response = await SendAsync(target.Id);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await GetUserAsync(target.Id)).IsSuspended.Should().BeFalse();
    }

    [Fact]
    public async Task LiftingTwice_ReturnsNoContentBothTimes()
    {
        await AddCallerAsync(Permission.SuspendUsers);
        User target = await AddSuspendedUserAsync();

        using HttpResponseMessage first = await SendAsync(target.Id);
        using HttpResponseMessage second = await SendAsync(target.Id);

        first.StatusCode.Should().Be(HttpStatusCode.NoContent);
        second.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    private static string PathOf(Guid id) => $"/api/v1/admin/users/{id}/suspension";

    private static async Task ShouldBeProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        response.StatusCode.Should().Be(status);
        ProblemDetails problem = (await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken))!;
        problem.Extensions["code"].Should().BeOfType<JsonElement>().Which.GetString().Should().Be(code);
    }

    private async Task<HttpResponseMessage> SendAsync(Guid id)
    {
        using HttpRequestMessage request = new(HttpMethod.Delete, PathOf(id));
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer", TestJwt.Create(DateTimeOffset.UtcNow, subject: "identity-id"));

        return await _client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private async Task AddCallerAsync(params string[] permissions)
    {
        Role callerRole = Role.Create("Caller Role", permissions);
        await _factory.ExecuteDbContextAsync(dbContext =>
        {
            dbContext.Add(callerRole);
            return dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        await AddUserAsync("identity-id", "username", callerRole.Id);
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

    private async Task<User> AddSuspendedUserAsync()
    {
        User user = User.Create("identity-id-2", "username-2", "Display Name", null);
        user.Suspend("reason", user.Id, ChocoApiFactory.StartTime);

        await _factory.ExecuteDbContextAsync(dbContext =>
        {
            dbContext.Add(user);
            return dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        return user;
    }

    private Task DeleteUserAsync(Guid id)
    {
        return _factory.ExecuteDbContextAsync(dbContext =>
        {
            dbContext.Remove(dbContext.Set<User>().Single(user => user.Id == id));
            return dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
    }

    private Task<User> GetUserAsync(Guid id)
    {
        return _factory.ExecuteDbContextAsync(dbContext => dbContext.Set<User>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync(user => user.Id == id, TestContext.Current.CancellationToken));
    }
}
