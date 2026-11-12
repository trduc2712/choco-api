using System.Net;
using System.Net.Http.Json;

using Choco.Domain.AuditLogs;
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
        System.Text.Json.JsonElement body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(TestContext.Current.CancellationToken);
        AuditLogEntry entry = (await ReadEntriesAsync()).Should().ContainSingle().Subject;
        ShouldBeEntry(entry, administrator.Id, AuditAction.RoleCreated, AuditTargetType.Role, body.GetProperty("id").GetGuid(), detail: "Role: Reviewer");
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
