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

public sealed class UpdateRoleTests : IClassFixture<ChocoApiFactory>, IAsyncLifetime
{
    private static readonly Guid UnknownId = new("00000000-0000-0000-0000-000000000001");

    private readonly ChocoApiFactory _factory;
    private readonly HttpClient _client;

    public UpdateRoleTests(ChocoApiFactory factory)
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
        using HttpRequestMessage request = new(HttpMethod.Put, PathOf(UnknownId)) { Content = JsonContent.Create(Body("Name")) };

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Member_ReturnsForbiddenAndChangesNothing()
    {
        Role target = await AddRoleAsync("Target", Permission.HandleReports);
        await AddUserAsync("identity-id", "username");

        using HttpResponseMessage response = await SendAsync(target.Id, Body("Renamed"));

        await ShouldBeProblemAsync(response, HttpStatusCode.Forbidden, "Permission.Denied");
        (await GetRoleAsync(target.Id)).Name.Should().Be("Target");
    }

    [Fact]
    public async Task UserWithoutUpdateRoles_ReturnsForbidden()
    {
        Role target = await AddRoleAsync("Target", Permission.HandleReports);
        await AddCallerAsync(Permission.ReadRoles, Permission.CreateRoles, Permission.DeleteRoles, Permission.AssignRoles, Permission.HandleReports);

        using HttpResponseMessage response = await SendAsync(target.Id, Body("Renamed", Permission.HandleReports));

        await ShouldBeProblemAsync(response, HttpStatusCode.Forbidden, "Permission.Denied");
        (await GetRoleAsync(target.Id)).Name.Should().Be("Target");
    }

    [Fact]
    public async Task EmptyId_ReturnsBadRequest()
    {
        await AddCallerAsync(Permission.UpdateRoles);

        using HttpResponseMessage response = await SendAsync(Guid.Empty, Body("Name"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UnknownRole_ReturnsNotFound()
    {
        await AddCallerAsync(Permission.UpdateRoles);

        using HttpResponseMessage response = await SendAsync(UnknownId, Body("Name"));

        await ShouldBeProblemAsync(response, HttpStatusCode.NotFound, "Role.NotFound");
    }

    [Fact]
    public async Task CallerHoldingEveryPermissionInvolved_ReplacesTheNameAndPermissions()
    {
        Role target = await AddRoleAsync("Target", Permission.HandleReports);
        await AddCallerAsync(Permission.UpdateRoles, Permission.HandleReports, Permission.DeleteAnyPost);

        using HttpResponseMessage response = await SendAsync(target.Id, Body("  Renamed  ", Permission.DeleteAnyPost, Permission.DeleteAnyPost));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        body.GetProperty("id").GetGuid().Should().Be(target.Id);
        body.GetProperty("name").GetString().Should().Be("Renamed");
        body.GetProperty("permissions").EnumerateArray().Select(permission => permission.GetString()).Should().Equal(Permission.DeleteAnyPost);
        Role stored = await GetRoleAsync(target.Id);
        stored.Name.Should().Be("Renamed");
        stored.Permissions.Should().BeEquivalentTo([Permission.DeleteAnyPost]);
    }

    [Fact]
    public async Task SameBodyTwice_ChangesNothingTheSecondTime()
    {
        Role target = await AddRoleAsync("Target", Permission.HandleReports);
        await AddCallerAsync(Permission.UpdateRoles, Permission.HandleReports);

        using HttpResponseMessage first = await SendAsync(target.Id, Body("Renamed", Permission.HandleReports));
        using HttpResponseMessage second = await SendAsync(target.Id, Body("Renamed", Permission.HandleReports));

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        Role stored = await GetRoleAsync(target.Id);
        stored.Name.Should().Be("Renamed");
        stored.Permissions.Should().BeEquivalentTo([Permission.HandleReports]);
    }

    [Fact]
    public async Task RemovingEveryPermission_LeavesTheRoleWithNone()
    {
        Role target = await AddRoleAsync("Target", Permission.HandleReports);
        await AddCallerAsync(Permission.UpdateRoles, Permission.HandleReports);

        using HttpResponseMessage response = await SendAsync(target.Id, Body("Target"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await GetRoleAsync(target.Id)).Permissions.Should().BeEmpty();
    }

    [Fact]
    public async Task NewPermissionTheCallerDoesNotHold_ReturnsForbiddenAndChangesNothing()
    {
        Role target = await AddRoleAsync("Target", Permission.HandleReports);
        await AddCallerAsync(Permission.UpdateRoles, Permission.HandleReports);

        using HttpResponseMessage response = await SendAsync(target.Id, Body("Renamed", Permission.HandleReports, Permission.ReadAuditLog));

        await ShouldBeProblemAsync(response, HttpStatusCode.Forbidden, "Permission.Denied");
        Role stored = await GetRoleAsync(target.Id);
        stored.Name.Should().Be("Target");
        stored.Permissions.Should().BeEquivalentTo([Permission.HandleReports]);
    }

    [Fact]
    public async Task CurrentPermissionTheCallerDoesNotHold_ReturnsForbiddenAndChangesNothing()
    {
        Role target = await AddRoleAsync("Target", Permission.HandleReports, Permission.ReadAuditLog);
        await AddCallerAsync(Permission.UpdateRoles, Permission.HandleReports);

        using HttpResponseMessage response = await SendAsync(target.Id, Body("Renamed", Permission.HandleReports));

        await ShouldBeProblemAsync(response, HttpStatusCode.Forbidden, "Permission.Denied");
        Role stored = await GetRoleAsync(target.Id);
        stored.Name.Should().Be("Target");
        stored.Permissions.Should().BeEquivalentTo([Permission.HandleReports, Permission.ReadAuditLog]);
    }

    [Fact]
    public async Task RoleAssignedToTheCaller_ReturnsForbiddenAndChangesNothing()
    {
        Role callerRole = await AddRoleAsync("Caller Role", Permission.UpdateRoles, Permission.HandleReports);
        await AddUserAsync("identity-id", "username", callerRole.Id);

        using HttpResponseMessage response = await SendAsync(callerRole.Id, Body("Renamed", Permission.UpdateRoles));

        await ShouldBeProblemAsync(response, HttpStatusCode.Forbidden, "Role.HeldByCaller");
        Role stored = await GetRoleAsync(callerRole.Id);
        stored.Name.Should().Be("Caller Role");
        stored.Permissions.Should().BeEquivalentTo([Permission.UpdateRoles, Permission.HandleReports]);
    }

    [Fact]
    public async Task DefaultRole_CanBeChangedAndKeepsItsKeyAndDefaultFlag()
    {
        Guid memberRoleId = await GetRoleIdAsync(SystemRoles.Member);
        await AddCallerAsync(Permission.UpdateRoles, Permission.ReadAuditLog);

        using HttpResponseMessage response = await SendAsync(memberRoleId, Body("Everyone", Permission.ReadAuditLog));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        Role stored = await GetRoleAsync(memberRoleId);
        stored.Name.Should().Be("Everyone");
        stored.SystemKey.Should().Be(SystemRoles.Member);
        stored.IsDefault.Should().BeTrue();
        stored.Permissions.Should().BeEquivalentTo([Permission.ReadAuditLog]);
    }

    [Fact]
    public async Task DefaultRole_CannotGetAPermissionTheCallerDoesNotHold()
    {
        Guid memberRoleId = await GetRoleIdAsync(SystemRoles.Member);
        await AddCallerAsync(Permission.UpdateRoles);

        using HttpResponseMessage response = await SendAsync(memberRoleId, Body("Member", Permission.ReadAuditLog));

        await ShouldBeProblemAsync(response, HttpStatusCode.Forbidden, "Permission.Denied");
        (await GetRoleAsync(memberRoleId)).Permissions.Should().BeEmpty();
    }

    [Fact]
    public async Task ChangeAppliesToHoldersOnTheirNextRequest()
    {
        Role target = await AddRoleAsync("Target", Permission.ReadAuditLog);
        await AddCallerAsync(Permission.UpdateRoles, Permission.ReadAuditLog);
        await AddUserAsync("identity-id-2", "username-2", target.Id);

        using HttpResponseMessage before = await GetAuditLogsAsync("identity-id-2");
        using HttpResponseMessage update = await SendAsync(target.Id, Body("Target"));
        using HttpResponseMessage after = await GetAuditLogsAsync("identity-id-2");

        before.StatusCode.Should().Be(HttpStatusCode.OK);
        update.StatusCode.Should().Be(HttpStatusCode.OK);
        after.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("Other")]
    [InlineData("other")]
    [InlineData("  OTHER  ")]
    public async Task NameOfAnotherRoleIgnoringCase_ReturnsConflictAndChangesNothing(string name)
    {
        Role target = await AddRoleAsync("Target");
        await AddRoleAsync("Other");
        await AddCallerAsync(Permission.UpdateRoles);

        using HttpResponseMessage response = await SendAsync(target.Id, Body(name));

        await ShouldBeProblemAsync(response, HttpStatusCode.Conflict, "Role.NameTaken");
        (await GetRoleAsync(target.Id)).Name.Should().Be("Target");
    }

    [Fact]
    public async Task OwnNameInAnotherCase_IsAccepted()
    {
        Role target = await AddRoleAsync("Target");
        await AddCallerAsync(Permission.UpdateRoles);

        using HttpResponseMessage response = await SendAsync(target.Id, Body("TARGET"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await GetRoleAsync(target.Id)).Name.Should().Be("TARGET");
    }

    [Fact]
    public async Task BlankName_ReturnsBadRequest()
    {
        Role target = await AddRoleAsync("Target");
        await AddCallerAsync(Permission.UpdateRoles);

        using HttpResponseMessage response = await SendAsync(target.Id, Body(" "));

        await ShouldBeValidationProblemAsync(response, "Name");
    }

    [Fact]
    public async Task PermissionOutsideTheCatalog_ReturnsBadRequest()
    {
        Role target = await AddRoleAsync("Target");
        await AddCallerAsync(Permission.UpdateRoles);

        using HttpResponseMessage response = await SendAsync(target.Id, Body("Target", "unknown.permission"));

        await ShouldBeValidationProblemAsync(response, "Permissions");
    }

    [Fact]
    public async Task MissingPermissionList_ReturnsBadRequest()
    {
        Role target = await AddRoleAsync("Target");
        await AddCallerAsync(Permission.UpdateRoles);

        using HttpResponseMessage response = await SendAsync(target.Id, new { name = "Target" });

        await ShouldBeValidationProblemAsync(response, "Permissions");
    }

    private static string PathOf(Guid id) => $"/api/v1/admin/roles/{id}";

    private static object Body(string name, params string[] permissions) => new { name, permissions };

    private static HttpRequestMessage CreateRequest(HttpMethod method, string path, string identityId, object? body = null)
    {
        HttpRequestMessage request = new(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer", TestJwt.Create(DateTimeOffset.UtcNow, subject: identityId));

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

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

    private async Task<HttpResponseMessage> SendAsync(Guid id, object body)
    {
        using HttpRequestMessage request = CreateRequest(HttpMethod.Put, PathOf(id), "identity-id", body);

        return await _client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private async Task<HttpResponseMessage> GetAuditLogsAsync(string identityId)
    {
        using HttpRequestMessage request = CreateRequest(HttpMethod.Get, "/api/v1/admin/audit-logs", identityId);

        return await _client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private async Task AddCallerAsync(params string[] permissions)
    {
        Role callerRole = await AddRoleAsync("Caller Role", permissions);
        await AddUserAsync("identity-id", "username", callerRole.Id);
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

    private async Task<Role> AddRoleAsync(string name, params string[] permissions)
    {
        Role role = Role.Create(name, permissions);

        await _factory.ExecuteDbContextAsync(dbContext =>
        {
            dbContext.Add(role);
            return dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        return role;
    }

    private Task<Role> GetRoleAsync(Guid id)
    {
        return _factory.ExecuteDbContextAsync(dbContext => dbContext.Set<Role>()
            .AsNoTracking()
            .SingleAsync(role => role.Id == id, TestContext.Current.CancellationToken));
    }

    private Task<Guid> GetRoleIdAsync(string systemKey)
    {
        return _factory.ExecuteDbContextAsync(dbContext => dbContext.Set<Role>()
            .Where(role => role.SystemKey == systemKey)
            .Select(role => role.Id)
            .SingleAsync(TestContext.Current.CancellationToken));
    }
}
