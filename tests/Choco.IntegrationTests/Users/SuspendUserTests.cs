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

public sealed class SuspendUserTests : IClassFixture<ChocoApiFactory>, IAsyncLifetime
{
    private static readonly Guid UnknownId = new("00000000-0000-0000-0000-000000000001");

    private readonly ChocoApiFactory _factory;
    private readonly HttpClient _client;

    public SuspendUserTests(ChocoApiFactory factory)
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
        using HttpRequestMessage request = new(HttpMethod.Put, PathOf(UnknownId)) { Content = JsonContent.Create(Body("reason")) };

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Member_ReturnsForbiddenAndSuspendsNothing()
    {
        await AddUserAsync("identity-id", "username");
        User target = await AddUserAsync("identity-id-2", "username-2");

        using HttpResponseMessage response = await SendAsync(target.Id, Body("reason"));

        await ShouldBeProblemAsync(response, HttpStatusCode.Forbidden, "Permission.Denied");
        (await GetUserAsync(target.Id)).IsSuspended.Should().BeFalse();
    }

    [Fact]
    public async Task UserWithAnotherPermissionButNotSuspendUsers_ReturnsForbidden()
    {
        await AddCallerAsync(Permission.ReadAnyUser, Permission.AssignRoles, Permission.HandleReports);
        User target = await AddUserAsync("identity-id-2", "username-2");

        using HttpResponseMessage response = await SendAsync(target.Id, Body("reason"));

        await ShouldBeProblemAsync(response, HttpStatusCode.Forbidden, "Permission.Denied");
        (await GetUserAsync(target.Id)).IsSuspended.Should().BeFalse();
    }

    [Fact]
    public async Task EmptyUserId_ReturnsBadRequest()
    {
        await AddCallerAsync(Permission.SuspendUsers);

        using HttpResponseMessage response = await SendAsync(Guid.Empty, Body("reason"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task BlankReason_ReturnsBadRequestAndSuspendsNothing(string reason)
    {
        await AddCallerAsync(Permission.SuspendUsers);
        User target = await AddUserAsync("identity-id-2", "username-2");

        using HttpResponseMessage response = await SendAsync(target.Id, Body(reason));

        await ShouldBeValidationProblemAsync(response, "reason");
        (await GetUserAsync(target.Id)).IsSuspended.Should().BeFalse();
    }

    [Fact]
    public async Task MissingReason_ReturnsBadRequest()
    {
        await AddCallerAsync(Permission.SuspendUsers);
        User target = await AddUserAsync("identity-id-2", "username-2");

        using HttpResponseMessage response = await SendAsync(target.Id, new { });

        await ShouldBeValidationProblemAsync(response, "reason");
    }

    [Fact]
    public async Task ReasonOverMaxLength_ReturnsBadRequestAndAtMaxLengthIsAccepted()
    {
        await AddCallerAsync(Permission.SuspendUsers);
        User target = await AddUserAsync("identity-id-2", "username-2");

        using HttpResponseMessage tooLong = await SendAsync(target.Id, Body(new string('r', User.SuspensionReasonMaxLength + 1)));
        using HttpResponseMessage atLimit = await SendAsync(target.Id, Body(new string('r', User.SuspensionReasonMaxLength)));

        await ShouldBeValidationProblemAsync(tooLong, "reason");
        atLimit.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task UnknownUser_ReturnsNotFound()
    {
        await AddCallerAsync(Permission.SuspendUsers);

        using HttpResponseMessage response = await SendAsync(UnknownId, Body("reason"));

        await ShouldBeProblemAsync(response, HttpStatusCode.NotFound, "User.NotFound");
    }

    [Fact]
    public async Task DeletedUser_ReturnsNotFound()
    {
        await AddCallerAsync(Permission.SuspendUsers);
        User target = await AddUserAsync("identity-id-2", "username-2");
        await DeleteUserAsync(target.Id);

        using HttpResponseMessage response = await SendAsync(target.Id, Body("reason"));

        await ShouldBeProblemAsync(response, HttpStatusCode.NotFound, "User.NotFound");
    }

    [Fact]
    public async Task Suspending_RecordsTheTrimmedReasonWhoAndWhen()
    {
        User caller = await AddCallerAsync(Permission.SuspendUsers);
        User target = await AddUserAsync("identity-id-2", "username-2");

        using HttpResponseMessage response = await SendAsync(target.Id, Body("  repeated spam  "));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        User stored = await GetUserAsync(target.Id);
        stored.IsSuspended.Should().BeTrue();
        stored.SuspensionReason.Should().Be("repeated spam");
        stored.SuspendedBy.Should().Be(caller.Id);
        stored.SuspendedAt.Should().Be(_factory.TimeProvider.GetUtcNow());
    }

    [Fact]
    public async Task BodyUserId_IsIgnoredAndTheRouteWins()
    {
        await AddCallerAsync(Permission.SuspendUsers);
        User target = await AddUserAsync("identity-id-2", "username-2");
        User other = await AddUserAsync("identity-id-3", "username-3");

        using HttpResponseMessage response = await SendAsync(target.Id, new { userId = other.Id, reason = "reason" });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await GetUserAsync(target.Id)).IsSuspended.Should().BeTrue();
        (await GetUserAsync(other.Id)).IsSuspended.Should().BeFalse();
    }

    [Fact]
    public async Task SameReasonTwice_KeepsTheFirstSuspension()
    {
        await AddCallerAsync(Permission.SuspendUsers);
        User target = await AddUserAsync("identity-id-2", "username-2");
        using HttpResponseMessage first = await SendAsync(target.Id, Body("reason"));
        DateTimeOffset firstTime = _factory.TimeProvider.GetUtcNow();
        _factory.TimeProvider.Advance(TimeSpan.FromHours(1));

        using HttpResponseMessage second = await SendAsync(target.Id, Body("reason"));

        first.StatusCode.Should().Be(HttpStatusCode.NoContent);
        second.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await GetUserAsync(target.Id)).SuspendedAt.Should().Be(firstTime);
    }

    [Fact]
    public async Task AnotherReason_ReplacesTheReasonAndRecordsTheTimeAgain()
    {
        await AddCallerAsync(Permission.SuspendUsers);
        User target = await AddUserAsync("identity-id-2", "username-2");
        using HttpResponseMessage first = await SendAsync(target.Id, Body("reason"));
        _factory.TimeProvider.Advance(TimeSpan.FromHours(1));

        using HttpResponseMessage second = await SendAsync(target.Id, Body("other reason"));

        second.StatusCode.Should().Be(HttpStatusCode.NoContent);
        User stored = await GetUserAsync(target.Id);
        stored.SuspensionReason.Should().Be("other reason");
        stored.SuspendedAt.Should().Be(_factory.TimeProvider.GetUtcNow());
        first.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task SuspendingYourself_ReturnsBadRequestAndSuspendsNothing()
    {
        User caller = await AddCallerAsync(Permission.SuspendUsers);

        using HttpResponseMessage response = await SendAsync(caller.Id, Body("reason"));

        await ShouldBeProblemAsync(response, HttpStatusCode.BadRequest, "User.CannotSuspendSelf");
        (await GetUserAsync(caller.Id)).IsSuspended.Should().BeFalse();
    }

    [Fact]
    public async Task UserWithAPermissionTheCallerLacks_ReturnsForbiddenAndSuspendsNothing()
    {
        await AddCallerAsync(Permission.SuspendUsers);
        Role auditor = await AddRoleAsync("Auditor", Permission.ReadAuditLog);
        User target = await AddUserAsync("identity-id-2", "username-2", auditor.Id);

        using HttpResponseMessage response = await SendAsync(target.Id, Body("reason"));

        await ShouldBeProblemAsync(response, HttpStatusCode.Forbidden, "Permission.Denied");
        (await GetUserAsync(target.Id)).IsSuspended.Should().BeFalse();
    }

    [Fact]
    public async Task UserWhosePermissionsTheCallerHolds_CanBeSuspended()
    {
        await AddCallerAsync(Permission.SuspendUsers, Permission.ReadAuditLog);
        Role auditor = await AddRoleAsync("Auditor", Permission.ReadAuditLog);
        User target = await AddUserAsync("identity-id-2", "username-2", auditor.Id);

        using HttpResponseMessage response = await SendAsync(target.Id, Body("reason"));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await GetUserAsync(target.Id)).IsSuspended.Should().BeTrue();
    }

    [Fact]
    public async Task AdministratorSuspendsAnotherAdministrator_ReturnsNoContent()
    {
        Guid adminRoleId = await GetRoleIdAsync(SystemRoles.Admin);
        await AddUserAsync("identity-id", "username", adminRoleId);
        User target = await AddUserAsync("identity-id-2", "username-2", adminRoleId);

        using HttpResponseMessage response = await SendAsync(target.Id, Body("reason"));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await GetUserAsync(target.Id)).IsSuspended.Should().BeTrue();
    }

    [Fact]
    public async Task LastAdministratorWhoIsNotSuspended_ReturnsConflictAndSuspendsNothing()
    {
        Guid adminRoleId = await GetRoleIdAsync(SystemRoles.Admin);
        User firstAdmin = await AddUserAsync("identity-id-2", "username-2", adminRoleId);
        User secondAdmin = await AddUserAsync("identity-id-3", "username-3", adminRoleId);
        await AddCallerAsync([.. Permission.All]);

        using HttpResponseMessage first = await SendAsync(firstAdmin.Id, Body("reason"));
        using HttpResponseMessage second = await SendAsync(secondAdmin.Id, Body("reason"));

        first.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await ShouldBeProblemAsync(second, HttpStatusCode.Conflict, "User.LastAdministrator");
        (await GetUserAsync(secondAdmin.Id)).IsSuspended.Should().BeFalse();
    }

    [Fact]
    public async Task AdministratorWhoIsAlreadySuspended_CanBeSuspendedAgainEvenAsTheOnlyOne()
    {
        Guid adminRoleId = await GetRoleIdAsync(SystemRoles.Admin);
        User onlyAdmin = await AddUserAsync("identity-id-2", "username-2", adminRoleId);
        await AddCallerAsync([.. Permission.All]);
        await SuspendDirectlyAsync(onlyAdmin.Id);

        using HttpResponseMessage response = await SendAsync(onlyAdmin.Id, Body("other reason"));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await GetUserAsync(onlyAdmin.Id)).SuspensionReason.Should().Be("other reason");
    }

    private static string PathOf(Guid id) => $"/api/v1/admin/users/{id}/suspension";

    private static object Body(string reason) => new { reason };

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
        using HttpRequestMessage request = new(HttpMethod.Put, PathOf(id)) { Content = JsonContent.Create(body) };
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer", TestJwt.Create(DateTimeOffset.UtcNow, subject: "identity-id"));

        return await _client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private async Task<User> AddCallerAsync(params string[] permissions)
    {
        Role callerRole = await AddRoleAsync("Caller Role", permissions);

        return await AddUserAsync("identity-id", "username", callerRole.Id);
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

    private Task DeleteUserAsync(Guid id)
    {
        return _factory.ExecuteDbContextAsync(dbContext =>
        {
            dbContext.Remove(dbContext.Set<User>().Single(user => user.Id == id));
            return dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
    }

    private Task SuspendDirectlyAsync(Guid id)
    {
        return _factory.ExecuteDbContextAsync(dbContext =>
        {
            dbContext.Set<User>().Single(user => user.Id == id).Suspend("reason", id, ChocoApiFactory.StartTime);
            return dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
    }

    private Task<User> GetUserAsync(Guid id)
    {
        return _factory.ExecuteDbContextAsync(dbContext => dbContext.Set<User>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync(user => user.Id == id, TestContext.Current.CancellationToken));
    }

    private Task<Guid> GetRoleIdAsync(string systemKey)
    {
        return _factory.ExecuteDbContextAsync(dbContext => dbContext.Set<Role>()
            .Where(role => role.SystemKey == systemKey)
            .Select(role => role.Id)
            .SingleAsync(TestContext.Current.CancellationToken));
    }
}
