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

public sealed class CreateRoleTests : IClassFixture<ChocoApiFactory>, IAsyncLifetime
{
    private const string Path = "/api/v1/admin/roles";

    private readonly ChocoApiFactory _factory;
    private readonly HttpClient _client;

    public CreateRoleTests(ChocoApiFactory factory)
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
        using HttpRequestMessage request = new(HttpMethod.Post, Path) { Content = JsonContent.Create(Body("Name")) };

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Member_ReturnsForbiddenAndCreatesNothing()
    {
        await AddUserAsync("identity-id", "username");
        using HttpRequestMessage request = CreateRequest(Body("Name"));

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await ShouldBeProblemAsync(response, HttpStatusCode.Forbidden, "Permission.Denied");
        (await FindRolesNamedAsync("Name")).Should().BeEmpty();
    }

    [Fact]
    public async Task UserWithoutCreateRoles_ReturnsForbidden()
    {
        Role role = await AddRoleAsync("reader-key", "Reader", [Permission.ReadRoles, Permission.AssignRoles, Permission.UpdateRoles, Permission.DeleteRoles]);
        await AddUserAsync("identity-id", "username", role.Id);
        using HttpRequestMessage request = CreateRequest(Body("Name"));

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await ShouldBeProblemAsync(response, HttpStatusCode.Forbidden, "Permission.Denied");
        (await FindRolesNamedAsync("Name")).Should().BeEmpty();
    }

    [Fact]
    public async Task PermissionTheCallerDoesNotHold_ReturnsForbiddenAndCreatesNothing()
    {
        Role role = await AddRoleAsync("creator-key", "Creator", [Permission.CreateRoles, Permission.HandleReports]);
        await AddUserAsync("identity-id", "username", role.Id);
        using HttpRequestMessage request = CreateRequest(Body("Name", Permission.HandleReports, Permission.ReadAuditLog));

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await ShouldBeProblemAsync(response, HttpStatusCode.Forbidden, "Permission.Denied");
        (await FindRolesNamedAsync("Name")).Should().BeEmpty();
    }

    [Fact]
    public async Task OnlyPermissionsTheCallerHolds_CreatesTheRole()
    {
        Role role = await AddRoleAsync("creator-key", "Creator", [Permission.CreateRoles, Permission.HandleReports, Permission.DeleteAnyPost]);
        await AddUserAsync("identity-id", "username", role.Id);
        using HttpRequestMessage request = CreateRequest(Body("Name", Permission.HandleReports));

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await FindRolesNamedAsync("Name")).Should().ContainSingle();
    }

    [Fact]
    public async Task Administrator_CreatesTheRoleAndReturnsItWithItsLocation()
    {
        await AddUserAsync("identity-id", "username", await GetAdministratorRoleIdAsync());
        using HttpRequestMessage request = CreateRequest(Body("  Reviewer  ", Permission.HandleReports, Permission.DeleteAnyPost, Permission.HandleReports));

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Guid id = body.GetProperty("id").GetGuid();
        body.GetProperty("name").GetString().Should().Be("Reviewer");
        body.GetProperty("systemKey").ValueKind.Should().Be(JsonValueKind.Null);
        body.GetProperty("isDefault").GetBoolean().Should().BeFalse();
        body.GetProperty("permissions").EnumerateArray().Select(permission => permission.GetString())
            .Should().Equal(Permission.DeleteAnyPost, Permission.HandleReports);
        response.Headers.Location!.AbsolutePath.Should().Be($"{Path}/{id}");
        Role stored = (await FindRolesNamedAsync("Reviewer")).Should().ContainSingle().Subject;
        stored.Id.Should().Be(id);
        stored.SystemKey.Should().BeNull();
        stored.IsDefault.Should().BeFalse();
        stored.Permissions.Should().BeEquivalentTo([Permission.DeleteAnyPost, Permission.HandleReports]);
    }

    [Fact]
    public async Task CreatedRole_CanBeReadByIdAndHasNoHolders()
    {
        await AddUserAsync("identity-id", "username", await GetAdministratorRoleIdAsync());
        using HttpRequestMessage request = CreateRequest(Body("Reviewer", Permission.HandleReports));
        using HttpResponseMessage created = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        using HttpRequestMessage readRequest = new(HttpMethod.Get, created.Headers.Location);
        readRequest.Headers.Authorization = request.Headers.Authorization;

        using HttpResponseMessage read = await _client.SendAsync(readRequest, TestContext.Current.CancellationToken);

        read.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement body = await read.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Guid createdId = body.GetProperty("id").GetGuid();
        int holders = await _factory.ExecuteDbContextAsync(dbContext => dbContext.Set<User>()
            .SelectMany(user => user.Roles)
            .CountAsync(userRole => userRole.RoleId == createdId, TestContext.Current.CancellationToken));
        holders.Should().Be(0);
    }

    [Fact]
    public async Task RoleWithoutPermissions_IsCreated()
    {
        await AddUserAsync("identity-id", "username", await GetAdministratorRoleIdAsync());
        using HttpRequestMessage request = CreateRequest(Body("Empty Role"));

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        body.GetProperty("permissions").GetArrayLength().Should().Be(0);
    }

    [Theory]
    [InlineData("Administrator")]
    [InlineData("administrator")]
    [InlineData("ADMINISTRATOR")]
    [InlineData("  administrator  ")]
    public async Task NameOfAnotherRoleIgnoringCase_ReturnsConflictAndCreatesNothing(string name)
    {
        await AddUserAsync("identity-id", "username", await GetAdministratorRoleIdAsync());
        using HttpRequestMessage request = CreateRequest(Body(name));

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await ShouldBeProblemAsync(response, HttpStatusCode.Conflict, "Role.NameTaken");
        (await FindRolesNamedAsync("Administrator")).Should().ContainSingle();
    }

    [Fact]
    public async Task SameBodyTwice_ReturnsConflictTheSecondTime()
    {
        await AddUserAsync("identity-id", "username", await GetAdministratorRoleIdAsync());
        using HttpRequestMessage first = CreateRequest(Body("Reviewer"));
        using HttpRequestMessage second = CreateRequest(Body("Reviewer"));

        using HttpResponseMessage firstResponse = await _client.SendAsync(first, TestContext.Current.CancellationToken);
        using HttpResponseMessage secondResponse = await _client.SendAsync(second, TestContext.Current.CancellationToken);

        firstResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        await ShouldBeProblemAsync(secondResponse, HttpStatusCode.Conflict, "Role.NameTaken");
    }

    [Fact]
    public async Task BlankName_ReturnsBadRequest()
    {
        await AddUserAsync("identity-id", "username", await GetAdministratorRoleIdAsync());
        using HttpRequestMessage request = CreateRequest(Body(" "));

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await ShouldBeValidationProblemAsync(response, "Name");
    }

    [Fact]
    public async Task NameOverMaxLength_ReturnsBadRequest()
    {
        await AddUserAsync("identity-id", "username", await GetAdministratorRoleIdAsync());
        using HttpRequestMessage request = CreateRequest(Body(new string('n', Role.NameMaxLength + 1)));

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await ShouldBeValidationProblemAsync(response, "Name");
    }

    [Fact]
    public async Task NameAtMaxLength_IsCreated()
    {
        await AddUserAsync("identity-id", "username", await GetAdministratorRoleIdAsync());
        using HttpRequestMessage request = CreateRequest(Body(new string('n', Role.NameMaxLength)));

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task PermissionOutsideTheCatalog_ReturnsBadRequest()
    {
        await AddUserAsync("identity-id", "username", await GetAdministratorRoleIdAsync());
        using HttpRequestMessage request = CreateRequest(Body("Reviewer", "unknown.permission"));

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await ShouldBeValidationProblemAsync(response, "Permissions");
        (await FindRolesNamedAsync("Reviewer")).Should().BeEmpty();
    }

    [Fact]
    public async Task MissingPermissionList_ReturnsBadRequest()
    {
        await AddUserAsync("identity-id", "username", await GetAdministratorRoleIdAsync());
        using HttpRequestMessage request = CreateRequest(new { name = "Reviewer" });

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await ShouldBeValidationProblemAsync(response, "Permissions");
    }

    [Fact]
    public async Task InvalidBodyFromCallerWithoutPermission_ReturnsForbiddenBeforeValidating()
    {
        await AddUserAsync("identity-id", "username");
        using HttpRequestMessage request = CreateRequest(Body(" "));

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static object Body(string name, params string[] permissions) => new { name, permissions };

    private static HttpRequestMessage CreateRequest(object body)
    {
        HttpRequestMessage request = new(HttpMethod.Post, Path) { Content = JsonContent.Create(body) };
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer", TestJwt.Create(DateTimeOffset.UtcNow, subject: "identity-id"));

        return request;
    }

    private static async Task ShouldBeProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        response.StatusCode.Should().Be(status);
        ProblemDetails problem = (await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken))!;
        problem.Extensions["code"].Should().BeOfType<JsonElement>().Which.GetString().Should().Be(code);
    }

    private static async Task ShouldBeValidationProblemAsync(HttpResponseMessage response, string field)
    {
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        ValidationProblemDetails problem = (await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(TestContext.Current.CancellationToken))!;
        problem.Errors.Keys.Should().ContainSingle().Which.Should().BeEquivalentTo(field);
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

    private Task<Guid> GetAdministratorRoleIdAsync()
    {
        return _factory.ExecuteDbContextAsync(dbContext => dbContext.Set<Role>()
            .Where(role => role.SystemKey == SystemRoles.Admin)
            .Select(role => role.Id)
            .SingleAsync(TestContext.Current.CancellationToken));
    }

    private Task<List<Role>> FindRolesNamedAsync(string name)
    {
        return _factory.ExecuteDbContextAsync(dbContext => dbContext.Set<Role>()
            .AsNoTracking()
            .Where(role => role.Name == name)
            .ToListAsync(TestContext.Current.CancellationToken));
    }
}
