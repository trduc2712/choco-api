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

public sealed class GetRoleByIdTests : IClassFixture<ChocoApiFactory>, IAsyncLifetime
{
    private static readonly Guid UnknownId = new("00000000-0000-0000-0000-000000000001");

    private readonly ChocoApiFactory _factory;
    private readonly HttpClient _client;

    public GetRoleByIdTests(ChocoApiFactory factory)
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
        using HttpRequestMessage request = new(HttpMethod.Get, PathOf(UnknownId));

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Member_ReturnsForbidden()
    {
        await AddUserAsync("identity-id", "username");
        using HttpRequestMessage request = CreateRequest(UnknownId);

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        ProblemDetails problem = (await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken))!;
        problem.Extensions["code"].Should().BeOfType<JsonElement>().Which.GetString().Should().Be("Permission.Denied");
    }

    [Fact]
    public async Task UserWithOnlyAssignRoles_ReturnsForbidden()
    {
        Role role = await AddRoleAsync("assigner-key", "Assigner", [Permission.AssignRoles]);
        await AddUserAsync("identity-id", "username", role.Id);
        using HttpRequestMessage request = CreateRequest(role.Id);

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task EmptyId_ReturnsBadRequest()
    {
        await AddUserAsync("identity-id", "username", await AddReaderRoleAsync());
        using HttpRequestMessage request = CreateRequest(Guid.Empty);

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UnknownId_ReturnsNotFound()
    {
        await AddUserAsync("identity-id", "username", await AddReaderRoleAsync());
        using HttpRequestMessage request = CreateRequest(UnknownId);

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        ProblemDetails problem = (await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken))!;
        problem.Extensions["code"].Should().BeOfType<JsonElement>().Which.GetString().Should().Be("Role.NotFound");
    }

    [Fact]
    public async Task UserWithOnlyReadRoles_GetsTheRoleWithItsPermissionsSorted()
    {
        Role reader = await AddRoleAsync("reader-key", "Reader", [Permission.ReadRoles]);
        await AddUserAsync("identity-id", "username", reader.Id);
        Role role = await AddRoleAsync("custom-key", "Custom Role", [Permission.HandleReports, Permission.DeleteAnyPost]);
        using HttpRequestMessage request = CreateRequest(role.Id);

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        body.GetProperty("id").GetGuid().Should().Be(role.Id);
        body.GetProperty("systemKey").GetString().Should().Be("custom-key");
        body.GetProperty("name").GetString().Should().Be("Custom Role");
        body.GetProperty("isDefault").GetBoolean().Should().BeFalse();
        body.GetProperty("permissions").EnumerateArray().Select(permission => permission.GetString())
            .Should().Equal(Permission.DeleteAnyPost, Permission.HandleReports);
    }

    [Fact]
    public async Task DefaultRole_IsMarkedAsDefaultWithoutPermissions()
    {
        await AddUserAsync("identity-id", "username", await AddReaderRoleAsync());
        Guid memberRoleId = await _factory.ExecuteDbContextAsync(dbContext => dbContext.Set<Role>()
            .Where(role => role.SystemKey == SystemRoles.Member)
            .Select(role => role.Id)
            .SingleAsync(TestContext.Current.CancellationToken));
        using HttpRequestMessage request = CreateRequest(memberRoleId);

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        body.GetProperty("systemKey").GetString().Should().Be(SystemRoles.Member);
        body.GetProperty("isDefault").GetBoolean().Should().BeTrue();
        body.GetProperty("permissions").GetArrayLength().Should().Be(0);
    }

    private static string PathOf(Guid id) => $"/api/v1/admin/roles/{id}";

    private static HttpRequestMessage CreateRequest(Guid id)
    {
        HttpRequestMessage request = new(HttpMethod.Get, PathOf(id));
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer", TestJwt.Create(DateTimeOffset.UtcNow, subject: "identity-id"));

        return request;
    }

    private async Task<Guid> AddReaderRoleAsync()
    {
        Role reader = await AddRoleAsync("reader-key", "Reader", [Permission.ReadRoles]);

        return reader.Id;
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
}
