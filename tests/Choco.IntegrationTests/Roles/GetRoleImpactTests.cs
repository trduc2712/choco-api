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

public sealed class GetRoleImpactTests(ChocoApiFactory factory) : IClassFixture<ChocoApiFactory>, IAsyncLifetime
{
    private readonly HttpClient _client = factory.CreateClient();

    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync()
    {
        _client.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task ChangedPermissions_ReturnHolderAndRetentionCountsWithoutUpdatingRole()
    {
        (Role target, Role member) = await ArrangeRolesAsync();
        await SetDefaultPermissionsAsync(member.Id, Permission.HandleReports);

        using HttpResponseMessage response = await GetAsync(target.Id, "?permissions=users.read-any&permissions=content.read-any");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        body.GetProperty("affectedHolderCount").GetInt32().Should().Be(2);
        body.GetProperty("addedPermissions").EnumerateArray().Select(item => item.GetString())
            .Should().Equal(Permission.ReadAnyContent, Permission.ReadAnyUser);
        JsonElement[] removed = body.GetProperty("removedPermissions").EnumerateArray().ToArray();
        removed.Select(item => item.GetProperty("code").GetString()).Should().Equal(Permission.DeleteAnyPost, Permission.HandleReports);
        removed.Select(item => item.GetProperty("keptByHolderCount").GetInt32()).Should().Equal(1, 2);
        Role stored = await factory.ExecuteDbContextAsync(dbContext => dbContext.Set<Role>()
            .SingleAsync(role => role.Id == target.Id, TestContext.Current.CancellationToken));
        stored.Permissions.Should().BeEquivalentTo([Permission.DeleteAnyPost, Permission.HandleReports]);
    }

    [Fact]
    public async Task SamePermissions_ReturnsNoAffectedHolders()
    {
        (Role target, _) = await ArrangeRolesAsync();

        using HttpResponseMessage response = await GetAsync(target.Id, "?permissions=reports.handle&permissions=posts.delete-any&permissions=reports.handle");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        body.GetProperty("affectedHolderCount").GetInt32().Should().Be(0);
        body.GetProperty("addedPermissions").GetArrayLength().Should().Be(0);
        body.GetProperty("removedPermissions").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task DefaultRoleWithoutQueryParameters_PreviewsRemovingAllPermissionsForAllUsers()
    {
        (_, Role member) = await ArrangeRolesAsync();
        await SetDefaultPermissionsAsync(member.Id, Permission.DeleteAnyPost);

        using HttpResponseMessage response = await GetAsync(member.Id, "");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        body.GetProperty("affectedHolderCount").GetInt32().Should().Be(3);
        body.GetProperty("addedPermissions").GetArrayLength().Should().Be(0);
        JsonElement removed = body.GetProperty("removedPermissions").EnumerateArray().Should().ContainSingle().Subject;
        removed.GetProperty("code").GetString().Should().Be(Permission.DeleteAnyPost);
        removed.GetProperty("keptByHolderCount").GetInt32().Should().Be(3);
    }

    [Fact]
    public async Task CallerWhoHoldsTheRole_ReturnsForbidden()
    {
        await ArrangeRolesAsync();
        Guid adminId = await factory.ExecuteDbContextAsync(dbContext => dbContext.Set<Role>()
            .Where(role => role.SystemKey == SystemRoles.Admin)
            .Select(role => role.Id)
            .SingleAsync(TestContext.Current.CancellationToken));

        using HttpResponseMessage response = await GetAsync(adminId, "");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        ProblemDetails problem = (await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken))!;
        problem.Extensions["code"].Should().BeOfType<JsonElement>().Which.GetString().Should().Be("Role.HeldByCaller");
    }

    [Fact]
    public async Task CallerWithoutUpdatePermission_ReturnsForbidden()
    {
        (Role target, _) = await ArrangeRolesAsync();

        using HttpResponseMessage response = await GetAsync(target.Id, "", "identity-id-2");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        ProblemDetails problem = (await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken))!;
        problem.Extensions["code"].Should().BeOfType<JsonElement>().Which.GetString().Should().Be("Permission.Denied");
    }

    [Fact]
    public async Task UnknownProposedPermission_ReturnsBadRequest()
    {
        (Role target, _) = await ArrangeRolesAsync();

        using HttpResponseMessage response = await GetAsync(target.Id, "?permissions=unknown.permission");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        ValidationProblemDetails problem =
            (await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(TestContext.Current.CancellationToken))!;
        problem.Errors.Keys.Should().ContainSingle().Which.Should().Be("permissions");
    }

    private async Task<HttpResponseMessage> GetAsync(Guid id, string query, string identityId = "identity-id")
    {
        using HttpRequestMessage request = new(HttpMethod.Get, $"/api/v1/admin/roles/{id}/impact{query}");
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer", TestJwt.Create(DateTimeOffset.UtcNow, subject: identityId));

        return await _client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private Task<(Role Target, Role Member)> ArrangeRolesAsync()
    {
        return factory.ExecuteDbContextAsync(async dbContext =>
        {
            Role admin = await dbContext.Set<Role>().SingleAsync(role => role.SystemKey == SystemRoles.Admin, TestContext.Current.CancellationToken);
            Role member = await dbContext.Set<Role>().SingleAsync(role => role.IsDefault, TestContext.Current.CancellationToken);
            Role target = Role.Create("Target Role", [Permission.DeleteAnyPost, Permission.HandleReports]);
            Role alternate = Role.Create("Alternate Role", [Permission.DeleteAnyPost]);
            User caller = User.Create("identity-id", "username", "Display Name", null);
            User first = User.Create("identity-id-2", "username-2", "Display Name 2", null);
            User second = User.Create("identity-id-3", "username-3", "Display Name 3", null);
            caller.AssignRole(admin.Id, null, ChocoApiFactory.StartTime);
            first.AssignRole(target.Id, null, ChocoApiFactory.StartTime);
            first.AssignRole(alternate.Id, null, ChocoApiFactory.StartTime);
            second.AssignRole(target.Id, null, ChocoApiFactory.StartTime);
            dbContext.AddRange(target, alternate, caller, first, second);
            await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

            return (target, member);
        });
    }

    private Task SetDefaultPermissionsAsync(Guid id, string permission)
    {
        return factory.ExecuteDbContextAsync(async dbContext =>
        {
            Role role = await dbContext.Set<Role>().SingleAsync(item => item.Id == id, TestContext.Current.CancellationToken);
            role.Update(role.Name, [permission]);
            return await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
    }
}
