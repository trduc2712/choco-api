using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Choco.Domain.AuditLogs;
using Choco.Domain.Roles;
using Choco.Domain.Users;

namespace Choco.IntegrationTests.Audit;

public sealed class RoleAuditTests(ChocoApiFactory factory) : AuditTestsBase(factory)
{
    [Fact]
    public async Task AssignRole_RecordsRoleAssignedWithTheRoleName()
    {
        Guid administratorRoleId = await GetAdministratorRoleIdAsync();
        User administrator = await AddUserAsync("identity-id", "username", administratorRoleId);
        User member = await AddUserAsync("identity-id-2", "username-2");
        using HttpRequestMessage request = Request(HttpMethod.Put, $"/api/v1/users/{member.Id}/roles/{administratorRoleId}");

        using HttpResponseMessage response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        AuditLogEntry entry = (await ReadEntriesAsync()).Should().ContainSingle().Subject;
        ShouldBeEntry(entry, administrator.Id, AuditAction.RoleAssigned, AuditTargetType.User, member.Id, member.Id, "Role: Administrator");
    }

    [Fact]
    public async Task CreateRole_RecordsRoleCreatedWithTheRoleName()
    {
        Guid administratorRoleId = await GetAdministratorRoleIdAsync();
        User administrator = await AddUserAsync("identity-id", "username", administratorRoleId);
        using HttpRequestMessage request = Request(HttpMethod.Post, "/api/v1/admin/roles", new { name = "Reviewer", permissions = new[] { "reports.handle" } });

        using HttpResponseMessage response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        AuditLogEntry entry = (await ReadEntriesAsync()).Should().ContainSingle().Subject;
        ShouldBeEntry(entry, administrator.Id, AuditAction.RoleCreated, AuditTargetType.Role, body.GetProperty("id").GetGuid(), detail: "Role: Reviewer");
        entry.Changes.Should().BeEquivalentTo(new[]
        {
            new AuditChange("name", After: "Reviewer"),
            new AuditChange("permissions", Added: ["reports.handle"], Removed: [])
        });
    }

    [Fact]
    public async Task UpdateRole_RecordsRoleUpdatedWithTheNewRoleName()
    {
        User administrator = await AddUserAsync("identity-id", "username", await GetAdministratorRoleIdAsync());
        Role target = Role.Create("Reviewer", ["audit-log.read"]);
        await Factory.ExecuteDbContextAsync(dbContext =>
        {
            dbContext.Add(target);
            return dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        using HttpRequestMessage request = Request(HttpMethod.Put, $"/api/v1/admin/roles/{target.Id}", new { name = "Senior Reviewer", permissions = new[] { "reports.handle" } });

        using HttpResponseMessage response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        AuditLogEntry entry = (await ReadEntriesAsync()).Should().ContainSingle().Subject;
        ShouldBeEntry(entry, administrator.Id, AuditAction.RoleUpdated, AuditTargetType.Role, target.Id, detail: "Role: Senior Reviewer");
        entry.Changes.Should().BeEquivalentTo(new[]
        {
            new AuditChange("name", "Reviewer", "Senior Reviewer"),
            new AuditChange("permissions", Added: ["reports.handle"], Removed: ["audit-log.read"])
        });

        using HttpRequestMessage read = Request(HttpMethod.Get, $"/api/v1/admin/audit-logs?targetId={target.Id}");
        using HttpResponseMessage auditResponse = await Client.SendAsync(read, TestContext.Current.CancellationToken);
        auditResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement auditBody = await auditResponse.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        JsonElement changes = auditBody.GetProperty("items").EnumerateArray().Single().GetProperty("changes");
        changes[0].GetProperty("before").GetString().Should().Be("Reviewer");
        changes[1].GetProperty("removed").EnumerateArray().Single().GetString().Should().Be("audit-log.read");
    }

    [Fact]
    public async Task DeleteRole_RecordsRoleDeletedWithTheRoleName()
    {
        User administrator = await AddUserAsync("identity-id", "username", await GetAdministratorRoleIdAsync());
        Role target = Role.Create("Reviewer", ["audit-log.read"]);
        await Factory.ExecuteDbContextAsync(dbContext =>
        {
            dbContext.Add(target);
            return dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        using HttpRequestMessage request = Request(HttpMethod.Delete, $"/api/v1/admin/roles/{target.Id}");

        using HttpResponseMessage response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        AuditLogEntry entry = (await ReadEntriesAsync()).Should().ContainSingle().Subject;
        ShouldBeEntry(entry, administrator.Id, AuditAction.RoleDeleted, AuditTargetType.Role, target.Id, detail: "Role: Reviewer");
        entry.Changes.Should().BeEquivalentTo(new[]
        {
            new AuditChange("name", Before: "Reviewer"),
            new AuditChange("permissions", Added: [], Removed: ["audit-log.read"])
        });
    }

    [Fact]
    public async Task RevokeRole_RecordsRoleRevokedWithTheRoleName()
    {
        Guid administratorRoleId = await GetAdministratorRoleIdAsync();
        User administrator = await AddUserAsync("identity-id", "username", administratorRoleId);
        User other = await AddUserAsync("identity-id-2", "username-2", administratorRoleId);
        using HttpRequestMessage request = Request(HttpMethod.Delete, $"/api/v1/users/{other.Id}/roles/{administratorRoleId}");

        using HttpResponseMessage response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        AuditLogEntry entry = (await ReadEntriesAsync()).Should().ContainSingle().Subject;
        ShouldBeEntry(entry, administrator.Id, AuditAction.RoleRevoked, AuditTargetType.User, other.Id, other.Id, "Role: Administrator");
    }

    [Fact]
    public async Task RevokeOwnRole_RecordsTheRolesTheActorHeldBeforeTheChange()
    {
        Guid administratorRoleId = await GetAdministratorRoleIdAsync();
        User administrator = await AddUserAsync("identity-id", "username", administratorRoleId);
        await AddUserAsync("identity-id-2", "username-2", administratorRoleId);
        using HttpRequestMessage request = Request(HttpMethod.Delete, $"/api/v1/users/{administrator.Id}/roles/{administratorRoleId}");

        using HttpResponseMessage response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        AuditLogEntry entry = (await ReadEntriesAsync()).Should().ContainSingle().Subject;
        entry.Action.Should().Be(AuditAction.RoleRevoked);
        entry.ActorRoles.Should().Equal("Administrator", "Member");
        entry.AffectedUserId.Should().BeNull();
    }
}
