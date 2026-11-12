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

public sealed class GetPermissionsTests : IClassFixture<ChocoApiFactory>, IAsyncLifetime
{
    private const string Path = "/api/v1/admin/permissions";

    private readonly ChocoApiFactory _factory;
    private readonly HttpClient _client;

    public GetPermissionsTests(ChocoApiFactory factory)
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
        Role role = await AddRoleAsync("reviewer-key", "Reviewer", Permission.ReadAuditLog);
        await AddUserAsync("identity-id", "username", role.Id);
        using HttpRequestMessage request = CreateRequest();

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UserWithOnlyAssignRoles_ReturnsForbidden()
    {
        Role role = await AddRoleAsync("assigner-key", "Assigner", Permission.AssignRoles);
        await AddUserAsync("identity-id", "username", role.Id);
        using HttpRequestMessage request = CreateRequest();

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UserWithOnlyReadRoles_ListsTheCatalog()
    {
        Role role = await AddRoleAsync("reader-key", "Reader", Permission.ReadRoles);
        await AddUserAsync("identity-id", "username", role.Id);
        using HttpRequestMessage request = CreateRequest();

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Administrator_ListsEveryPermissionOfTheCatalogInAlphabeticalOrder()
    {
        Guid adminRoleId = await _factory.ExecuteDbContextAsync(dbContext => dbContext.Set<Role>()
            .Where(role => role.SystemKey == SystemRoles.Admin)
            .Select(role => role.Id)
            .SingleAsync(TestContext.Current.CancellationToken));
        await AddUserAsync("identity-id", "username", adminRoleId);
        using HttpRequestMessage request = CreateRequest();

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        body.EnumerateArray().Select(item => item.GetProperty("code").GetString())
            .Should().Equal(Permission.All.Order(StringComparer.Ordinal));
    }

    private static HttpRequestMessage CreateRequest()
    {
        HttpRequestMessage request = new(HttpMethod.Get, Path);
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer", TestJwt.Create(DateTimeOffset.UtcNow, subject: "identity-id"));

        return request;
    }

    private async Task<Role> AddRoleAsync(string systemKey, string name, string permission)
    {
        Role role = Role.CreateSystem(new SystemRoleDefinition(systemKey, name, IsDefault: false, Permissions: [permission]));

        await _factory.ExecuteDbContextAsync(dbContext =>
        {
            dbContext.Add(role);
            return dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        return role;
    }

    private async Task AddUserAsync(string identityId, string username, Guid? roleId = null)
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
    }
}
