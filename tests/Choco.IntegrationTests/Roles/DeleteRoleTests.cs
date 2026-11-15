using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using Choco.Domain.AuditLogs;
using Choco.Domain.Roles;
using Choco.Domain.Users;
using Choco.IntegrationTests.Authentication;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Choco.IntegrationTests.Roles;

public sealed class DeleteRoleTests : IClassFixture<ChocoApiFactory>, IAsyncLifetime
{
    private static readonly Guid UnknownId = new("00000000-0000-0000-0000-000000000001");

    private readonly ChocoApiFactory _factory;
    private readonly HttpClient _client;

    public DeleteRoleTests(ChocoApiFactory factory)
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
    public async Task Member_ReturnsForbiddenAndKeepsTheRole()
    {
        Role target = await AddRoleAsync("Target");
        await AddUserAsync("identity-id", "username");

        using HttpResponseMessage response = await SendAsync(target.Id);

        await ShouldBeProblemAsync(response, HttpStatusCode.Forbidden, "Permission.Denied");
        (await FindRoleAsync(target.Id)).Should().NotBeNull();
    }

    [Fact]
    public async Task UserWithoutDeleteRoles_ReturnsForbiddenAndKeepsTheRole()
    {
        Role target = await AddRoleAsync("Target");
        await AddCallerAsync(Permission.ReadRoles, Permission.CreateRoles, Permission.UpdateRoles, Permission.AssignRoles);

        using HttpResponseMessage response = await SendAsync(target.Id);

        await ShouldBeProblemAsync(response, HttpStatusCode.Forbidden, "Permission.Denied");
        (await FindRoleAsync(target.Id)).Should().NotBeNull();
    }

    [Fact]
    public async Task EmptyId_ReturnsBadRequest()
    {
        await AddCallerAsync(Permission.DeleteRoles);

        using HttpResponseMessage response = await SendAsync(Guid.Empty);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UnknownRole_ReturnsNotFound()
    {
        await AddCallerAsync(Permission.DeleteRoles);

        using HttpResponseMessage response = await SendAsync(UnknownId);

        await ShouldBeProblemAsync(response, HttpStatusCode.NotFound, "Role.NotFound");
    }

    [Fact]
    public async Task RoleWithoutHolders_IsDeleted()
    {
        Role target = await AddRoleAsync("Target", Permission.HandleReports);
        await AddCallerAsync(Permission.DeleteRoles, Permission.HandleReports);

        using HttpResponseMessage response = await SendAsync(target.Id);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await FindRoleAsync(target.Id)).Should().BeNull();
    }

    [Fact]
    public async Task DeletingTwice_ReturnsNotFoundTheSecondTime()
    {
        Role target = await AddRoleAsync("Target");
        await AddCallerAsync(Permission.DeleteRoles);

        using HttpResponseMessage first = await SendAsync(target.Id);
        using HttpResponseMessage second = await SendAsync(target.Id);

        first.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await ShouldBeProblemAsync(second, HttpStatusCode.NotFound, "Role.NotFound");
    }

    [Fact]
    public async Task Holders_LoseTheRoleAndItsPermissions()
    {
        Role target = await AddRoleAsync("Target", Permission.ReadAuditLog);
        await AddCallerAsync(Permission.DeleteRoles, Permission.ReadAuditLog);
        User holder = await AddUserAsync("identity-id-2", "username-2", target.Id);

        using HttpResponseMessage before = await GetAuditLogsAsync("identity-id-2");
        using HttpResponseMessage delete = await SendAsync(target.Id);
        using HttpResponseMessage after = await GetAuditLogsAsync("identity-id-2");

        before.StatusCode.Should().Be(HttpStatusCode.OK);
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);
        after.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        int assignments = await _factory.ExecuteDbContextAsync(dbContext => dbContext.Set<User>()
            .Where(user => user.Id == holder.Id)
            .SelectMany(user => user.Roles)
            .CountAsync(userRole => userRole.RoleId == target.Id, TestContext.Current.CancellationToken));
        assignments.Should().Be(0);
    }

    [Fact]
    public async Task DefaultRole_ReturnsConflictAndKeepsIt()
    {
        Guid memberRoleId = await GetRoleIdAsync(SystemRoles.Member);
        await AddCallerAsync(Permission.DeleteRoles);

        using HttpResponseMessage response = await SendAsync(memberRoleId);

        await ShouldBeProblemAsync(response, HttpStatusCode.Conflict, "Role.DefaultNotDeletable");
        (await FindRoleAsync(memberRoleId)).Should().NotBeNull();
    }

    [Fact]
    public async Task SystemRole_ReturnsConflictAndKeepsIt()
    {
        Guid adminRoleId = await GetRoleIdAsync(SystemRoles.Admin);
        await AddCallerAsync([.. Permission.All]);

        using HttpResponseMessage response = await SendAsync(adminRoleId);

        await ShouldBeProblemAsync(response, HttpStatusCode.Conflict, "Role.SystemNotDeletable");
        (await FindRoleAsync(adminRoleId)).Should().NotBeNull();
    }

    [Fact]
    public async Task RoleWithAPermissionTheCallerDoesNotHold_ReturnsForbiddenAndKeepsIt()
    {
        Role target = await AddRoleAsync("Target", Permission.HandleReports, Permission.ReadAuditLog);
        await AddCallerAsync(Permission.DeleteRoles, Permission.HandleReports);

        using HttpResponseMessage response = await SendAsync(target.Id);

        await ShouldBeProblemAsync(response, HttpStatusCode.Forbidden, "Permission.Denied");
        (await FindRoleAsync(target.Id)).Should().NotBeNull();
    }

    [Fact]
    public async Task RoleAssignedToTheCaller_ReturnsForbiddenAndKeepsIt()
    {
        Role callerRole = await AddRoleAsync("Caller Role", Permission.DeleteRoles);
        await AddUserAsync("identity-id", "username", callerRole.Id);

        using HttpResponseMessage response = await SendAsync(callerRole.Id);

        await ShouldBeProblemAsync(response, HttpStatusCode.Forbidden, "Role.HeldByCaller");
        (await FindRoleAsync(callerRole.Id)).Should().NotBeNull();
    }

    [Fact]
    public async Task ReassignmentToSameRole_ReturnsBadRequestAndKeepsSourceRole()
    {
        Role source = await AddRoleAsync("Source Role");
        await AddCallerAsync(Permission.DeleteRoles, Permission.AssignRoles);

        using HttpResponseMessage response = await SendAsync(source.Id, new { reassignTo = source.Id });

        await ShouldBeProblemAsync(response, HttpStatusCode.BadRequest, "Role.ReassignToSelf");
        (await FindRoleAsync(source.Id)).Should().NotBeNull();
    }

    [Fact]
    public async Task ReassignmentToDefaultRole_ReturnsBadRequestAndKeepsSourceRole()
    {
        Role source = await AddRoleAsync("Source Role");
        Guid defaultRoleId = await GetRoleIdAsync(SystemRoles.Member);
        await AddCallerAsync(Permission.DeleteRoles, Permission.AssignRoles);

        using HttpResponseMessage response = await SendAsync(source.Id, new { reassignTo = defaultRoleId });

        await ShouldBeProblemAsync(response, HttpStatusCode.BadRequest, "Role.DefaultNotAssignable");
        (await FindRoleAsync(source.Id)).Should().NotBeNull();
    }

    [Fact]
    public async Task ReassignmentToUnknownRole_ReturnsNotFoundAndKeepsSourceRole()
    {
        Role source = await AddRoleAsync("Source Role");
        await AddCallerAsync(Permission.DeleteRoles, Permission.AssignRoles);

        using HttpResponseMessage response = await SendAsync(source.Id, new { reassignTo = UnknownId });

        await ShouldBeProblemAsync(response, HttpStatusCode.NotFound, "Role.NotFound");
        (await FindRoleAsync(source.Id)).Should().NotBeNull();
    }

    [Fact]
    public async Task ReassignmentToCallerWithoutAssignPermission_ReturnsForbiddenAndKeepsSourceRole()
    {
        Role source = await AddRoleAsync("Source Role");
        Role replacement = await AddRoleAsync("Replacement Role");
        await AddCallerAsync(Permission.DeleteRoles);

        using HttpResponseMessage response = await SendAsync(source.Id, new { reassignTo = replacement.Id });

        await ShouldBeProblemAsync(response, HttpStatusCode.Forbidden, "Permission.Denied");
        (await FindRoleAsync(source.Id)).Should().NotBeNull();
    }

    [Fact]
    public async Task ReassignmentToCallerWithoutReplacementPermission_ReturnsForbiddenAndKeepsSourceRole()
    {
        Role source = await AddRoleAsync("Source Role");
        Role replacement = await AddRoleAsync("Replacement Role", Permission.HandleReports);
        await AddCallerAsync(Permission.DeleteRoles, Permission.AssignRoles);

        using HttpResponseMessage response = await SendAsync(source.Id, new { reassignTo = replacement.Id });

        await ShouldBeProblemAsync(response, HttpStatusCode.Forbidden, "Permission.Denied");
        (await FindRoleAsync(source.Id)).Should().NotBeNull();
    }

    [Fact]
    public async Task ReplacementRole_ReassignsHoldersAndAuditsEachAssignment()
    {
        Role source = await AddRoleAsync("Source Role");
        Role replacement = await AddRoleAsync("Replacement Role");
        await AddCallerAsync(Permission.DeleteRoles, Permission.AssignRoles);
        User first = User.Create("identity-id-2", "username-2", "Display Name 2", null);
        User second = User.Create("identity-id-3", "username-3", "Display Name 3", null);
        first.AssignRole(source.Id, null, ChocoApiFactory.StartTime);
        second.AssignRole(source.Id, null, ChocoApiFactory.StartTime);
        second.AssignRole(replacement.Id, null, ChocoApiFactory.StartTime);
        await _factory.ExecuteDbContextAsync(dbContext =>
        {
            dbContext.AddRange(first, second);
            return dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        using HttpResponseMessage response = await SendAsync(source.Id, new { reassignTo = replacement.Id });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await FindRoleAsync(source.Id)).Should().BeNull();
        var holders = await _factory.ExecuteDbContextAsync(dbContext => dbContext.Set<User>()
            .Where(user => user.Id == first.Id || user.Id == second.Id)
            .Select(user => new { user.Id, RoleIds = user.Roles.Select(role => role.RoleId).ToList() })
            .ToListAsync(TestContext.Current.CancellationToken));
        holders.Should().HaveCount(2).And.OnlyContain(holder => holder.RoleIds.Count == 1 && holder.RoleIds[0] == replacement.Id);
        List<AuditLogEntry> assignments = await _factory.ExecuteDbContextAsync(dbContext => dbContext.Set<AuditLogEntry>()
            .Where(entry => entry.Action == AuditAction.RoleAssigned)
            .ToListAsync(TestContext.Current.CancellationToken));
        assignments.Should().HaveCount(2);
        AuditLogEntry assignment = assignments.Single(entry => entry.TargetId == first.Id);
        assignment.TargetId.Should().Be(first.Id);
        assignment.AffectedUserId.Should().Be(first.Id);
        assignment.Changes.Should().BeEquivalentTo(new[] { new AuditChange("roles", Added: [replacement.Name]) });
        AuditLogEntry existingAssignment = assignments.Single(entry => entry.TargetId == second.Id);
        existingAssignment.AffectedUserId.Should().Be(second.Id);
        existingAssignment.Changes.Should().BeEmpty();
    }

    [Fact]
    public async Task ReasonAboveLimit_ReturnsValidationErrorAndKeepsTheRole()
    {
        Role target = await AddRoleAsync("Role Name");
        await AddCallerAsync(Permission.DeleteRoles);
        string reason = new('r', AuditLogEntry.ReasonMaxLength + 1);

        using HttpResponseMessage response = await SendAsync(target.Id, new { reason });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        ValidationProblemDetails problem =
            (await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(TestContext.Current.CancellationToken))!;
        problem.Errors.Keys.Should().ContainSingle().Which.Should().Be("reason");
        (await FindRoleAsync(target.Id)).Should().NotBeNull();
    }

    private static string PathOf(Guid id) => $"/api/v1/admin/roles/{id}";

    private static HttpRequestMessage CreateRequest(HttpMethod method, string path, string identityId)
    {
        HttpRequestMessage request = new(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer", TestJwt.Create(DateTimeOffset.UtcNow, subject: identityId));

        return request;
    }

    private static async Task ShouldBeProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        response.StatusCode.Should().Be(status);
        ProblemDetails problem = (await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken))!;
        problem.Extensions["code"].Should().BeOfType<JsonElement>().Which.GetString().Should().Be(code);
    }

    private async Task<HttpResponseMessage> SendAsync(Guid id, object? body = null)
    {
        using HttpRequestMessage request = CreateRequest(HttpMethod.Delete, PathOf(id), "identity-id");
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

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

    private Task<Role?> FindRoleAsync(Guid id)
    {
        return _factory.ExecuteDbContextAsync(dbContext => dbContext.Set<Role>()
            .AsNoTracking()
            .SingleOrDefaultAsync(role => role.Id == id, TestContext.Current.CancellationToken));
    }

    private Task<Guid> GetRoleIdAsync(string systemKey)
    {
        return _factory.ExecuteDbContextAsync(dbContext => dbContext.Set<Role>()
            .Where(role => role.SystemKey == systemKey)
            .Select(role => role.Id)
            .SingleAsync(TestContext.Current.CancellationToken));
    }
}
