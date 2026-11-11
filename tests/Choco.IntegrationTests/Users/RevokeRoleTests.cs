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

public sealed class RevokeRoleTests : IClassFixture<ChocoApiFactory>, IAsyncLifetime
{
    private static readonly Guid UnknownId = new("00000000-0000-0000-0000-000000000001");

    private readonly ChocoApiFactory _factory;
    private readonly HttpClient _client;

    public RevokeRoleTests(ChocoApiFactory factory)
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
        Guid adminRoleId = await GetRoleIdAsync(SystemRoles.Admin);
        User target = await AddUserAsync("identity-id-2", "username-2", adminRoleId);
        using HttpRequestMessage request = new(HttpMethod.Delete, $"/api/v1/users/{target.Id}/roles/{adminRoleId}");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CallerWithoutPermission_ReturnsForbiddenAndKeepsRole()
    {
        Guid adminRoleId = await GetRoleIdAsync(SystemRoles.Admin);
        await AddUserAsync("identity-id", "username");
        User target = await AddUserAsync("identity-id-2", "username-2", adminRoleId);
        using HttpRequestMessage request = CreateRequest(target.Id, adminRoleId);

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await ShouldBeProblemAsync(response, HttpStatusCode.Forbidden, "Permission.Denied");
        (await GetUserAsync(target.Id)).HoldsRole(adminRoleId).Should().BeTrue();
    }

    [Fact]
    public async Task CallerLackingAPermissionOfTheRole_ReturnsForbiddenAndKeepsRole()
    {
        Guid adminRoleId = await GetRoleIdAsync(SystemRoles.Admin);
        Guid roleManagerRoleId = await AddRoleManagerRoleAsync();
        await AddUserAsync("identity-id", "username", roleManagerRoleId);
        await AddUserAsync("identity-id-3", "username-3", adminRoleId);
        User target = await AddUserAsync("identity-id-2", "username-2", adminRoleId);
        using HttpRequestMessage request = CreateRequest(target.Id, adminRoleId);

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await ShouldBeProblemAsync(response, HttpStatusCode.Forbidden, "Permission.Denied");
        (await GetUserAsync(target.Id)).HoldsRole(adminRoleId).Should().BeTrue();
    }

    [Fact]
    public async Task AdminRevokesRoleFromAnotherAdmin_RemovesItAndReturnsNoContent()
    {
        Guid adminRoleId = await GetRoleIdAsync(SystemRoles.Admin);
        await AddUserAsync("identity-id", "username", adminRoleId);
        User target = await AddUserAsync("identity-id-2", "username-2", adminRoleId);
        using HttpRequestMessage request = CreateRequest(target.Id, adminRoleId);

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await GetUserAsync(target.Id)).Roles.Should().BeEmpty();
    }

    [Fact]
    public async Task RoleNotHeld_ReturnsNoContent()
    {
        Guid adminRoleId = await GetRoleIdAsync(SystemRoles.Admin);
        await AddUserAsync("identity-id", "username", adminRoleId);
        User target = await AddUserAsync("identity-id-2", "username-2");
        using HttpRequestMessage request = CreateRequest(target.Id, adminRoleId);

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await GetUserAsync(target.Id)).Roles.Should().BeEmpty();
    }

    [Fact]
    public async Task LastAdministratorRevokesOwnRole_ReturnsConflictAndKeepsRole()
    {
        Guid adminRoleId = await GetRoleIdAsync(SystemRoles.Admin);
        User caller = await AddUserAsync("identity-id", "username", adminRoleId);
        using HttpRequestMessage request = CreateRequest(caller.Id, adminRoleId);

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await ShouldBeProblemAsync(response, HttpStatusCode.Conflict, "Role.LastAdministrator");
        (await GetUserAsync(caller.Id)).HoldsRole(adminRoleId).Should().BeTrue();
    }

    [Fact]
    public async Task EmptyRoleId_ReturnsValidationProblem()
    {
        Guid adminRoleId = await GetRoleIdAsync(SystemRoles.Admin);
        await AddUserAsync("identity-id", "username", adminRoleId);
        User target = await AddUserAsync("identity-id-2", "username-2");
        using HttpRequestMessage request = CreateRequest(target.Id, Guid.Empty);

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        ValidationProblemDetails problem = (await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(TestContext.Current.CancellationToken))!;
        problem.Errors.Keys.Should().BeEquivalentTo(["roleId"]);
        problem.Extensions["code"].Should().BeOfType<JsonElement>().Which.GetString().Should().Be("Validation.Failed");
    }

    [Fact]
    public async Task DefaultRole_ReturnsBadRequest()
    {
        Guid adminRoleId = await GetRoleIdAsync(SystemRoles.Admin);
        Guid memberRoleId = await GetRoleIdAsync(SystemRoles.Member);
        await AddUserAsync("identity-id", "username", adminRoleId);
        User target = await AddUserAsync("identity-id-2", "username-2");
        using HttpRequestMessage request = CreateRequest(target.Id, memberRoleId);

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await ShouldBeProblemAsync(response, HttpStatusCode.BadRequest, "Role.DefaultNotAssignable");
    }

    [Fact]
    public async Task UnknownUser_ReturnsNotFound()
    {
        Guid adminRoleId = await GetRoleIdAsync(SystemRoles.Admin);
        await AddUserAsync("identity-id", "username", adminRoleId);
        using HttpRequestMessage request = CreateRequest(UnknownId, adminRoleId);

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await ShouldBeProblemAsync(response, HttpStatusCode.NotFound, "User.NotFound");
    }

    [Fact]
    public async Task DeletedUser_ReturnsNotFound()
    {
        Guid adminRoleId = await GetRoleIdAsync(SystemRoles.Admin);
        await AddUserAsync("identity-id", "username", adminRoleId);
        User target = await AddUserAsync("identity-id-2", "username-2", adminRoleId);
        await DeleteUserAsync(target.Id);
        using HttpRequestMessage request = CreateRequest(target.Id, adminRoleId);

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await ShouldBeProblemAsync(response, HttpStatusCode.NotFound, "User.NotFound");
    }

    [Fact]
    public async Task UnknownRole_ReturnsNotFound()
    {
        Guid adminRoleId = await GetRoleIdAsync(SystemRoles.Admin);
        await AddUserAsync("identity-id", "username", adminRoleId);
        User target = await AddUserAsync("identity-id-2", "username-2");
        using HttpRequestMessage request = CreateRequest(target.Id, UnknownId);

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await ShouldBeProblemAsync(response, HttpStatusCode.NotFound, "Role.NotFound");
    }

    private static HttpRequestMessage CreateRequest(Guid userId, Guid roleId)
    {
        HttpRequestMessage request = new(HttpMethod.Delete, $"/api/v1/users/{userId}/roles/{roleId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TestJwt.Create(DateTimeOffset.UtcNow));

        return request;
    }

    private static async Task ShouldBeProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        response.StatusCode.Should().Be(status);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        ProblemDetails problem = (await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken))!;
        problem.Extensions["code"].Should().BeOfType<JsonElement>().Which.GetString().Should().Be(code);
    }

    private async Task<Guid> AddRoleManagerRoleAsync()
    {
        Role role = Role.CreateSystem(new SystemRoleDefinition("role-manager", "Role Manager", IsDefault: false, Permissions: [Permission.AssignRoles]));
        await _factory.ExecuteDbContextAsync(dbContext =>
        {
            dbContext.Add(role);
            return dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        return role.Id;
    }

    private Task<Guid> GetRoleIdAsync(string systemKey)
    {
        return _factory.ExecuteDbContextAsync(dbContext => dbContext.Set<Role>()
            .Where(role => role.SystemKey == systemKey)
            .Select(role => role.Id)
            .SingleAsync(TestContext.Current.CancellationToken));
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
            .Include(user => user.Roles)
            .SingleAsync(user => user.Id == id, TestContext.Current.CancellationToken));
    }
}
