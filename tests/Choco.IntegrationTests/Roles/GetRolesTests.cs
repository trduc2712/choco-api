using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using Choco.Domain.Roles;
using Choco.Domain.Users;
using Choco.IntegrationTests.Authentication;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Choco.IntegrationTests.Roles;

public sealed class GetRolesTests : IClassFixture<ChocoApiFactory>, IAsyncLifetime
{
    private const string Path = "/api/v1/admin/roles";

    private readonly ChocoApiFactory _factory;
    private readonly HttpClient _client;

    public GetRolesTests(ChocoApiFactory factory)
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
        using HttpRequestMessage request = new(HttpMethod.Get, Path);

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Member_ReturnsForbidden()
    {
        await AddUserAsync("identity-id", "username");
        using HttpRequestMessage request = CreateRequest();

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        ProblemDetails problem = (await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken))!;
        problem.Extensions["code"].Should().BeOfType<JsonElement>().Which.GetString().Should().Be("Permission.Denied");
    }

    [Fact]
    public async Task UserWithAnotherPermissionButNotReadRoles_ReturnsForbidden()
    {
        Role role = await AddRoleAsync("reviewer-key", "Reviewer", [Permission.ReadAuditLog]);
        await AddUserAsync("identity-id", "username", role.Id);
        using HttpRequestMessage request = CreateRequest();

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UserWithOnlyAssignRoles_ReturnsForbidden()
    {
        Role role = await AddRoleAsync("assigner-key", "Assigner", [Permission.AssignRoles]);
        await AddUserAsync("identity-id", "username", role.Id);
        using HttpRequestMessage request = CreateRequest();

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UserWithOnlyReadRoles_ListsRoles()
    {
        Role role = await AddRoleAsync("reader-key", "Reader", [Permission.ReadRoles]);
        await AddUserAsync("identity-id", "username", role.Id);

        JsonElement roles = await GetJsonAsync();

        roles.EnumerateArray().Select(item => item.GetProperty("id").GetGuid()).Should().Contain(role.Id);
    }

    [Fact]
    public async Task Administrator_ListsTheSystemRolesWithTheirPermissions()
    {
        Guid adminRoleId = await GetRoleIdAsync(SystemRoles.Admin);
        Guid memberRoleId = await GetRoleIdAsync(SystemRoles.Member);
        await AddUserAsync("identity-id", "username", adminRoleId);
        User deleted = await AddUserAsync("identity-id-2", "username-2", adminRoleId);
        await _factory.ExecuteDbContextAsync(async dbContext =>
        {
            User user = await dbContext.Set<User>().SingleAsync(item => item.Id == deleted.Id, TestContext.Current.CancellationToken);
            dbContext.Remove(user);
            return await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        JsonElement[] roles = (await GetJsonAsync()).EnumerateArray().ToArray();

        roles.Select(role => role.GetProperty("name").GetString()).Should().Equal("Administrator", "Member", "Moderator");
        JsonElement admin = roles[0];
        admin.GetProperty("id").GetGuid().Should().Be(adminRoleId);
        admin.GetProperty("systemKey").GetString().Should().Be(SystemRoles.Admin);
        admin.GetProperty("isDefault").GetBoolean().Should().BeFalse();
        admin.GetProperty("permissions").EnumerateArray().Select(permission => permission.GetString())
            .Should().Equal(SystemRoles.All.Single(definition => definition.Key == SystemRoles.Admin).Permissions.Order(StringComparer.Ordinal));
        JsonElement member = roles[1];
        member.GetProperty("id").GetGuid().Should().Be(memberRoleId);
        member.GetProperty("systemKey").GetString().Should().Be(SystemRoles.Member);
        member.GetProperty("isDefault").GetBoolean().Should().BeTrue();
        member.GetProperty("permissions").GetArrayLength().Should().Be(0);
        member.GetProperty("holderCount").GetInt32().Should().Be(1);
        member.GetProperty("topHolders").GetArrayLength().Should().Be(0);
        member.GetProperty("color").GetString().Should().Be("gray");
        member.GetProperty("icon").GetString().Should().Be("user");
        admin.GetProperty("holderCount").GetInt32().Should().Be(1);
        admin.GetProperty("modifiedBy").ValueKind.Should().Be(JsonValueKind.Null);
        JsonElement adminHolder = admin.GetProperty("topHolders").EnumerateArray().Should().ContainSingle().Subject;
        adminHolder.GetProperty("username").GetString().Should().Be("username");
        adminHolder.GetProperty("displayName").GetString().Should().Be("Display Name");
        adminHolder.GetProperty("avatarUrl").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Administrator_SeesARoleAddedAfterTheSystemRolesWithItsPermissionsSorted()
    {
        Guid adminRoleId = await GetRoleIdAsync(SystemRoles.Admin);
        await AddUserAsync("identity-id", "username", adminRoleId);
        Role role = await AddRoleAsync("custom-key", "Custom Role", [Permission.HandleReports, Permission.DeleteAnyPost]);

        JsonElement[] roles = (await GetJsonAsync()).EnumerateArray().ToArray();

        JsonElement custom = roles.Single(item => item.GetProperty("id").GetGuid() == role.Id);
        custom.GetProperty("name").GetString().Should().Be("Custom Role");
        custom.GetProperty("systemKey").GetString().Should().Be("custom-key");
        custom.GetProperty("permissions").EnumerateArray().Select(permission => permission.GetString())
            .Should().Equal(Permission.DeleteAnyPost, Permission.HandleReports);
    }

    [Fact]
    public async Task MoreRoles_DoNotIncreaseDatabaseQueries()
    {
        Guid adminRoleId = await GetRoleIdAsync(SystemRoles.Admin);
        await AddUserAsync("identity-id", "username", adminRoleId);
        await AddRoleAsync("role-key-1", "Role Name 1", []);
        await GetJsonAsync();
        _factory.DbCommands.Reset();

        await GetJsonAsync();

        int oneExtraRoleQueries = _factory.DbCommands.Count;
        await AddRoleAsync("role-key-2", "Role Name 2", []);
        await AddRoleAsync("role-key-3", "Role Name 3", []);
        await AddRoleAsync("role-key-4", "Role Name 4", []);
        await AddRoleAsync("role-key-5", "Role Name 5", []);
        _factory.DbCommands.Reset();

        await GetJsonAsync();

        oneExtraRoleQueries.Should().BePositive();
        _factory.DbCommands.Count.Should().Be(oneExtraRoleQueries);
    }

    [Fact]
    public async Task MoreThanThreeHolders_ReturnsTheNewestThreeWithTheirProfiles()
    {
        Guid adminRoleId = await GetRoleIdAsync(SystemRoles.Admin);
        await AddUserAsync("identity-id", "username", adminRoleId);
        Role role = await AddRoleAsync("role-key", "Role Name", []);
        await _factory.ExecuteDbContextAsync(dbContext =>
        {
            User[] assignedUsers = Enumerable.Range(1, 4).Select(index =>
            {
                User user = User.Create($"holder-identity-id-{index}", $"holder-username-{index}",
                    $"Holder Display Name {index}", $"https://example.com/avatar-{index}.png");
                user.AssignRole(role.Id, null, ChocoApiFactory.StartTime.AddMinutes(index));

                return user;
            }).ToArray();
            dbContext.AddRange(assignedUsers);

            return dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        JsonElement roles = await GetJsonAsync();

        JsonElement summary = roles.EnumerateArray().Single(item => item.GetProperty("id").GetGuid() == role.Id);
        summary.GetProperty("holderCount").GetInt32().Should().Be(4);
        JsonElement[] holders = summary.GetProperty("topHolders").EnumerateArray().ToArray();
        holders.Select(holder => holder.GetProperty("username").GetString())
            .Should().Equal("holder-username-4", "holder-username-3", "holder-username-2");
        holders[0].GetProperty("displayName").GetString().Should().Be("Holder Display Name 4");
        holders[0].GetProperty("avatarUrl").GetString().Should().Be("https://example.com/avatar-4.png");
    }

    private HttpRequestMessage CreateRequest()
    {
        HttpRequestMessage request = new(HttpMethod.Get, Path);
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer", TestJwt.Create(DateTimeOffset.UtcNow, subject: "identity-id"));

        return request;
    }

    private async Task<JsonElement> GetJsonAsync()
    {
        using HttpRequestMessage request = CreateRequest();
        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        return await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
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

    private async Task<Role> AddRoleAsync(string systemKey, string name, string[] permissions)
    {
        Role role = Role.CreateSystem(new SystemRoleDefinition(systemKey, name, IsDefault: false, permissions));

        await _factory.ExecuteDbContextAsync(dbContext =>
        {
            dbContext.Add(role);
            return dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        return role;
    }

    private Task<Guid> GetRoleIdAsync(string systemKey)
    {
        return _factory.ExecuteDbContextAsync(dbContext => dbContext.Set<Role>()
            .Where(role => role.SystemKey == systemKey)
            .Select(role => role.Id)
            .SingleAsync(TestContext.Current.CancellationToken));
    }
}
