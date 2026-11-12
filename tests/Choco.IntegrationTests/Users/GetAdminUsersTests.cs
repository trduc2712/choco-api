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

public sealed class GetAdminUsersTests : IClassFixture<ChocoApiFactory>, IAsyncLifetime
{
    private const string Path = "/api/v1/admin/users";

    private readonly ChocoApiFactory _factory;
    private readonly HttpClient _client;

    public GetAdminUsersTests(ChocoApiFactory factory)
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

        using HttpResponseMessage response = await GetAsync(Path);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        ProblemDetails problem = (await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken))!;
        problem.Extensions["code"].Should().BeOfType<JsonElement>().Which.GetString().Should().Be("Permission.Denied");
    }

    [Fact]
    public async Task UserWithAnotherPermissionButNotReadAnyUser_ReturnsForbidden()
    {
        await AddCallerAsync(Permission.ReadAuditLog, Permission.ReadRoles, Permission.AssignRoles);

        using HttpResponseMessage response = await GetAsync(Path);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task InvalidQueryFromCallerWithoutPermission_ReturnsForbiddenBeforeValidating()
    {
        await AddUserAsync("identity-id", "username");

        using HttpResponseMessage response = await GetAsync($"{Path}?page=0");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UserWithOnlyReadAnyUser_ListsUsers()
    {
        await AddCallerAsync(Permission.ReadAnyUser);

        JsonElement body = await GetJsonAsync(Path);

        body.GetProperty("totalCount").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task Users_AreListedNewestSignUpFirstWithTheirFields()
    {
        await AddCallerAsync(Permission.ReadAnyUser);
        _factory.TimeProvider.Advance(TimeSpan.FromHours(1));
        User older = await AddUserAsync("identity-id-2", "username-2", displayName: "Display Name 2");
        _factory.TimeProvider.Advance(TimeSpan.FromHours(1));
        User newer = await AddUserAsync("identity-id-3", "username-3", displayName: "Display Name 3");

        JsonElement body = await GetJsonAsync(Path);

        JsonElement[] items = body.GetProperty("items").EnumerateArray().ToArray();
        items.Select(item => item.GetProperty("id").GetGuid()).Take(2).Should().Equal(newer.Id, older.Id);
        items.Select(item => item.GetProperty("username").GetString()).Last().Should().Be("username");
        JsonElement first = items[0];
        first.GetProperty("username").GetString().Should().Be("username-3");
        first.GetProperty("displayName").GetString().Should().Be("Display Name 3");
        first.GetProperty("createdAt").GetDateTimeOffset().Should().Be(ChocoApiFactory.StartTime.AddHours(2));
        first.GetProperty("isDeleted").GetBoolean().Should().BeFalse();
        first.GetProperty("roles").GetArrayLength().Should().Be(0);
        body.GetProperty("page").GetInt32().Should().Be(1);
        body.GetProperty("pageSize").GetInt32().Should().Be(20);
        body.GetProperty("totalCount").GetInt32().Should().Be(3);
    }

    [Fact]
    public async Task Entries_ListAssignedRolesByNameAndNotTheDefaultRole()
    {
        await AddCallerAsync(Permission.ReadAnyUser);
        Role zeta = await AddRoleAsync("Zeta Role");
        Role alpha = await AddRoleAsync("Alpha Role");
        User holder = await AddUserAsync("identity-id-2", "username-2", zeta.Id, alpha.Id);

        JsonElement body = await GetJsonAsync($"{Path}?q=username-2");

        JsonElement item = body.GetProperty("items").EnumerateArray().Should().ContainSingle().Subject;
        item.GetProperty("id").GetGuid().Should().Be(holder.Id);
        JsonElement[] roles = item.GetProperty("roles").EnumerateArray().ToArray();
        roles.Select(role => role.GetProperty("name").GetString()).Should().Equal("Alpha Role", "Zeta Role");
        roles.Select(role => role.GetProperty("id").GetGuid()).Should().Equal(alpha.Id, zeta.Id);
    }

    [Fact]
    public async Task DeletedUsers_AreHiddenByDefault()
    {
        await AddCallerAsync(Permission.ReadAnyUser);
        User deleted = await AddUserAsync("identity-id-2", "username-2");
        await DeleteUserAsync(deleted.Id);

        JsonElement body = await GetJsonAsync($"{Path}?q=username-2");

        body.GetProperty("items").GetArrayLength().Should().Be(0);
        body.GetProperty("totalCount").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task StateDeleted_ListsOnlyDeletedUsersAndMarksThem()
    {
        await AddCallerAsync(Permission.ReadAnyUser);
        User deleted = await AddUserAsync("identity-id-2", "username-2");
        await AddUserAsync("identity-id-3", "username-3");
        await DeleteUserAsync(deleted.Id);

        JsonElement body = await GetJsonAsync($"{Path}?state=deleted");

        JsonElement item = body.GetProperty("items").EnumerateArray().Should().ContainSingle().Subject;
        item.GetProperty("id").GetGuid().Should().Be(deleted.Id);
        item.GetProperty("isDeleted").GetBoolean().Should().BeTrue();
        body.GetProperty("totalCount").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task StateActive_ListsOnlyActiveUsers()
    {
        await AddCallerAsync(Permission.ReadAnyUser);
        User deleted = await AddUserAsync("identity-id-2", "username-2");
        await AddUserAsync("identity-id-3", "username-3");
        await DeleteUserAsync(deleted.Id);

        JsonElement body = await GetJsonAsync($"{Path}?state=active");

        body.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("username").GetString())
            .Should().BeEquivalentTo("username", "username-3");
    }

    [Fact]
    public async Task FilteringByARole_ListsItsHoldersOnly()
    {
        await AddCallerAsync(Permission.ReadAnyUser);
        Role role = await AddRoleAsync("Reviewer");
        User holder = await AddUserAsync("identity-id-2", "username-2", role.Id);
        await AddUserAsync("identity-id-3", "username-3");

        JsonElement body = await GetJsonAsync($"{Path}?roleId={role.Id}");

        body.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid()).Should().Equal(holder.Id);
        body.GetProperty("totalCount").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task FilteringByTheDefaultRole_ListsEveryActiveUser()
    {
        await AddCallerAsync(Permission.ReadAnyUser);
        await AddUserAsync("identity-id-2", "username-2");
        Guid memberRoleId = await GetRoleIdAsync(SystemRoles.Member);

        JsonElement body = await GetJsonAsync($"{Path}?roleId={memberRoleId}");

        body.GetProperty("totalCount").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task FilteringByAnUnknownRole_ListsNobody()
    {
        await AddCallerAsync(Permission.ReadAnyUser);

        JsonElement body = await GetJsonAsync($"{Path}?roleId=00000000-0000-0000-0000-000000000001");

        body.GetProperty("items").GetArrayLength().Should().Be(0);
        body.GetProperty("totalCount").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task Search_MatchesUsernameOrDisplayNameIgnoringCaseAndAccents()
    {
        await AddCallerAsync(Permission.ReadAnyUser);
        User byUsername = await AddUserAsync("identity-id-2", "Ngoc-Anh", displayName: "Display Name 2");
        User byDisplayName = await AddUserAsync("identity-id-3", "username-3", displayName: "Nguyễn Văn Đức");
        await AddUserAsync("identity-id-4", "username-4", displayName: "Display Name 4");

        JsonElement byUsernameBody = await GetJsonAsync($"{Path}?q=ngoc-anh");
        JsonElement byAccentBody = await GetJsonAsync($"{Path}?q=nguyen%20van%20duc");

        byUsernameBody.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid()).Should().Equal(byUsername.Id);
        byAccentBody.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid()).Should().Equal(byDisplayName.Id);
    }

    [Fact]
    public async Task Search_TreatsWildcardCharactersAsText()
    {
        await AddCallerAsync(Permission.ReadAnyUser);
        await AddUserAsync("identity-id-2", "username-2");

        JsonElement percent = await GetJsonAsync($"{Path}?q=%25");
        JsonElement underscore = await GetJsonAsync($"{Path}?q=_");

        percent.GetProperty("totalCount").GetInt32().Should().Be(0);
        underscore.GetProperty("totalCount").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task BlankSearch_IsIgnored()
    {
        await AddCallerAsync(Permission.ReadAnyUser);
        await AddUserAsync("identity-id-2", "username-2");

        JsonElement body = await GetJsonAsync($"{Path}?q=%20%20");

        body.GetProperty("totalCount").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task Filters_CombineWithAnd()
    {
        await AddCallerAsync(Permission.ReadAnyUser);
        Role role = await AddRoleAsync("Reviewer");
        User match = await AddUserAsync("identity-id-2", "username-2", role.Id);
        await AddUserAsync("identity-id-3", "username-3", role.Id);
        User deletedHolder = await AddUserAsync("identity-id-4", "username-4", role.Id);
        await DeleteUserAsync(deletedHolder.Id);

        JsonElement body = await GetJsonAsync($"{Path}?roleId={role.Id}&q=username-2&state=active");

        body.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid()).Should().Equal(match.Id);
    }

    [Fact]
    public async Task Users_AreReturnedByPageWithoutOverlapAndWithTheTotal()
    {
        await AddCallerAsync(Permission.ReadAnyUser);
        for (int index = 2; index <= 5; index++)
        {
            _factory.TimeProvider.Advance(TimeSpan.FromMinutes(1));
            await AddUserAsync($"identity-id-{index}", $"username-{index}");
        }

        JsonElement first = await GetJsonAsync($"{Path}?pageSize=2");
        JsonElement second = await GetJsonAsync($"{Path}?pageSize=2&page=2");
        JsonElement last = await GetJsonAsync($"{Path}?pageSize=2&page=3");
        JsonElement pastEnd = await GetJsonAsync($"{Path}?pageSize=2&page=99");

        static IEnumerable<string?> Names(JsonElement body) => body.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("username").GetString());

        Names(first).Should().Equal("username-5", "username-4");
        Names(second).Should().Equal("username-3", "username-2");
        Names(last).Should().Equal("username");
        pastEnd.GetProperty("items").GetArrayLength().Should().Be(0);
        pastEnd.GetProperty("page").GetInt32().Should().Be(99);
        first.GetProperty("totalCount").GetInt32().Should().Be(5);
        pastEnd.GetProperty("totalCount").GetInt32().Should().Be(5);
    }

    [Theory]
    [InlineData("state=suspended", "state")]
    [InlineData("state=Active", "state")]
    [InlineData("roleId=00000000-0000-0000-0000-000000000000", "roleId")]
    [InlineData("page=0", "page")]
    [InlineData("pageSize=0", "pageSize")]
    [InlineData("pageSize=51", "pageSize")]
    public async Task InvalidQuery_ReturnsBadRequestNamingTheField(string query, string field)
    {
        await AddCallerAsync(Permission.ReadAnyUser);

        using HttpResponseMessage response = await GetAsync($"{Path}?{query}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        ValidationProblemDetails problem = (await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(TestContext.Current.CancellationToken))!;
        problem.Errors.Keys.Should().ContainSingle().Which.Should().BeEquivalentTo(field);
    }

    [Fact]
    public async Task SearchOverMaxLength_ReturnsBadRequestAndAtMaxLengthIsAccepted()
    {
        await AddCallerAsync(Permission.ReadAnyUser);

        using HttpResponseMessage tooLong = await GetAsync($"{Path}?q={new string('a', Choco.Contract.Users.GetAdminUsersQuery.SearchMaxLength + 1)}");
        using HttpResponseMessage atLimit = await GetAsync($"{Path}?q={new string('a', Choco.Contract.Users.GetAdminUsersQuery.SearchMaxLength)}");

        tooLong.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        atLimit.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task<HttpResponseMessage> GetAsync(string path)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer", TestJwt.Create(DateTimeOffset.UtcNow, subject: "identity-id"));

        return await _client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private async Task<JsonElement> GetJsonAsync(string path)
    {
        using HttpResponseMessage response = await GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        return await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
    }

    private async Task AddCallerAsync(params string[] permissions)
    {
        Role callerRole = await AddRoleAsync("Caller Role", permissions);
        await AddUserAsync("identity-id", "username", callerRole.Id);
    }

    private async Task<User> AddUserAsync(string identityId, string username, params Guid[] roleIds)
    {
        return await AddUserAsync(identityId, username, null, roleIds);
    }

    private async Task<User> AddUserAsync(string identityId, string username, string? displayName, params Guid[] roleIds)
    {
        User user = User.Create(identityId, username, displayName ?? "Display Name", null);

        foreach (Guid roleId in roleIds)
        {
            user.AssignRole(roleId, null, ChocoApiFactory.StartTime);
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

    private Task<Guid> GetRoleIdAsync(string systemKey)
    {
        return _factory.ExecuteDbContextAsync(dbContext => dbContext.Set<Role>()
            .Where(role => role.SystemKey == systemKey)
            .Select(role => role.Id)
            .SingleAsync(TestContext.Current.CancellationToken));
    }
}
