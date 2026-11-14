using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using Choco.Contract.Users;
using Choco.Domain.Posts;
using Choco.Domain.Reports;
using Choco.Domain.Roles;
using Choco.Domain.Users;
using Choco.IntegrationTests.Authentication;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Choco.IntegrationTests.Users;

public sealed class GetAdminUserTests : IClassFixture<ChocoApiFactory>, IAsyncLifetime
{
    private static readonly Guid UnknownId = new("00000000-0000-0000-0000-000000000001");

    private readonly ChocoApiFactory _factory;
    private readonly HttpClient _client;

    public GetAdminUserTests(ChocoApiFactory factory)
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
        using HttpRequestMessage request = new(HttpMethod.Get, $"/api/v1/admin/users/{UnknownId}");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Member_ReturnsForbidden()
    {
        User target = await AddUserAsync("identity-id-2", "username-2");
        await AddUserAsync("identity-id", "username");

        using HttpResponseMessage response = await GetAsync(target.Id);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        ProblemDetails problem = (await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken))!;
        problem.Extensions["code"].Should().BeOfType<JsonElement>().Which.GetString().Should().Be("Permission.Denied");
    }

    [Fact]
    public async Task UserWithAnotherPermissionButNotReadAnyUser_ReturnsForbidden()
    {
        User target = await AddUserAsync("identity-id-2", "username-2");
        await AddCallerAsync(Permission.ReadAuditLog, Permission.HandleReports);

        using HttpResponseMessage response = await GetAsync(target.Id);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UnknownId_ReturnsNotFound()
    {
        await AddCallerAsync(Permission.ReadAnyUser);

        using HttpResponseMessage response = await GetAsync(UnknownId);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        ProblemDetails problem = (await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken))!;
        problem.Extensions["code"].Should().BeOfType<JsonElement>().Which.GetString().Should().Be("User.NotFound");
    }

    [Fact]
    public async Task EmptyId_ReturnsValidationProblemNamingTheUserId()
    {
        await AddCallerAsync(Permission.ReadAnyUser);

        using HttpResponseMessage response = await GetAsync(Guid.Empty);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        ValidationProblemDetails problem = (await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(TestContext.Current.CancellationToken))!;
        problem.Errors.Keys.Should().ContainSingle().Which.Should().Be("userId");
    }

    [Fact]
    public async Task InvalidIdFromCallerWithoutPermission_ReturnsForbiddenBeforeValidating()
    {
        await AddUserAsync("identity-id", "username");

        using HttpResponseMessage response = await GetAsync(Guid.Empty);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ActiveUser_ReturnsProfileRolesAndNoSuspension()
    {
        await AddCallerAsync(Permission.ReadAnyUser);
        Role role = await AddRoleAsync("Reviewer", Permission.HandleReports);
        User target = User.Create("identity-id-2", "username-2", "Target Name", "https://example.com/avatar.png");
        target.ChangeBio("A short bio");
        target.AssignRole(role.Id, null, ChocoApiFactory.StartTime);
        await AddAsync(target);

        AdminUserDetailResponse user = await GetUserAsync(target.Id);

        user.Id.Should().Be(target.Id);
        user.Username.Should().Be("username-2");
        user.DisplayName.Should().Be("Target Name");
        user.Bio.Should().Be("A short bio");
        user.AvatarUrl.Should().Be("https://example.com/avatar.png");
        user.CreatedAt.Should().Be(ChocoApiFactory.StartTime);
        user.Roles.Should().Equal(new AdminUserRoleResponse(role.Id, "Reviewer"));
        user.IsSuspended.Should().BeFalse();
        user.Suspension.Should().BeNull();
        user.IsDeleted.Should().BeFalse();
        user.DeletedAt.Should().BeNull();
        user.OpenReportCount.Should().Be(0);
    }

    [Fact]
    public async Task UserWithoutBioOrAvatar_ReturnsNulls()
    {
        await AddCallerAsync(Permission.ReadAnyUser);
        User target = await AddUserAsync("identity-id-2", "username-2");

        AdminUserDetailResponse user = await GetUserAsync(target.Id);

        user.Bio.Should().BeNull();
        user.AvatarUrl.Should().BeNull();
        user.Roles.Should().BeEmpty();
    }

    [Fact]
    public async Task SuspendedUserWithReports_ReturnsTheSuspensionAndTheOpenReportCount()
    {
        User caller = await AddCallerAsync(Permission.ReadAnyUser);
        User suspender = await AddUserAsync("identity-id-3", "suspender");
        User reporter = await AddUserAsync("identity-id-4", "reporter");
        User target = await AddUserAsync("identity-id-2", "username-2");
        _factory.TimeProvider.Advance(TimeSpan.FromHours(1));
        await SuspendAsync(target.Id, "Repeated spam", suspender.Id);
        await AddAsync(Report.CreateForUser(reporter.Id, target.Id, ReportReason.Spam, null, _factory.TimeProvider.GetUtcNow()).Value);
        await AddAsync(Report.CreateForUser(caller.Id, target.Id, ReportReason.Harassment, null, _factory.TimeProvider.GetUtcNow()).Value);

        AdminUserDetailResponse user = await GetUserAsync(target.Id);

        user.IsSuspended.Should().BeTrue();
        user.Suspension.Should().Be(new AdminUserSuspensionResponse(
            "Repeated spam", _factory.TimeProvider.GetUtcNow(), new AdminUserReferenceResponse(suspender.Id, "suspender")));
        user.OpenReportCount.Should().Be(2);
    }

    [Fact]
    public async Task OpenReportCount_LeavesOutResolvedReportsReportsAboutPostsAndOtherUsers()
    {
        User caller = await AddCallerAsync(Permission.ReadAnyUser);
        User reporter = await AddUserAsync("identity-id-3", "reporter");
        User target = await AddUserAsync("identity-id-2", "username-2");
        User other = await AddUserAsync("identity-id-4", "other");
        Post post = await AddPostAsync(target);
        Report open = Report.CreateForUser(reporter.Id, target.Id, ReportReason.Spam, null, _factory.TimeProvider.GetUtcNow()).Value;
        Report resolved = Report.CreateForUser(caller.Id, target.Id, ReportReason.Spam, null, _factory.TimeProvider.GetUtcNow()).Value;
        resolved.Resolve(ReportOutcome.Dismissed, caller.Id, _factory.TimeProvider.GetUtcNow());
        await AddAsync(open);
        await AddAsync(resolved);
        await AddAsync(Report.CreateForPost(reporter.Id, post.Id, target.Id, ReportReason.Spam, null, _factory.TimeProvider.GetUtcNow()).Value);
        await AddAsync(Report.CreateForUser(reporter.Id, other.Id, ReportReason.Spam, null, _factory.TimeProvider.GetUtcNow()).Value);

        AdminUserDetailResponse user = await GetUserAsync(target.Id);

        user.OpenReportCount.Should().Be(1);
    }

    [Fact]
    public async Task SuspenderWhoDeletedTheirAccount_StaysNamed()
    {
        await AddCallerAsync(Permission.ReadAnyUser);
        User suspender = await AddUserAsync("identity-id-3", "suspender");
        User target = await AddUserAsync("identity-id-2", "username-2");
        await SuspendAsync(target.Id, "reason", suspender.Id);
        await DeleteUserAsync(suspender.Id);

        AdminUserDetailResponse user = await GetUserAsync(target.Id);

        user.Suspension!.SuspendedBy.Should().Be(new AdminUserReferenceResponse(suspender.Id, "suspender"));
    }

    [Fact]
    public async Task DeletedUser_CanBeReadWithTheTimeOfTheDeletion()
    {
        await AddCallerAsync(Permission.ReadAnyUser);
        User target = await AddUserAsync("identity-id-2", "username-2");
        _factory.TimeProvider.Advance(TimeSpan.FromDays(1));
        await DeleteUserAsync(target.Id);

        AdminUserDetailResponse user = await GetUserAsync(target.Id);

        user.Username.Should().Be("username-2");
        user.IsDeleted.Should().BeTrue();
        user.DeletedAt.Should().Be(_factory.TimeProvider.GetUtcNow());
    }

    [Fact]
    public async Task Response_HasNoEmailAddress()
    {
        await AddCallerAsync(Permission.ReadAnyUser);
        User target = await AddUserAsync("identity-id-2", "username-2");

        using HttpResponseMessage response = await GetAsync(target.Id);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        body.EnumerateObject().Select(property => property.Name).Should().BeEquivalentTo(
            "id", "username", "displayName", "bio", "avatarUrl", "createdAt", "roles",
            "isSuspended", "suspension", "isDeleted", "deletedAt", "openReportCount");
    }

    private async Task<HttpResponseMessage> GetAsync(Guid userId)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, $"/api/v1/admin/users/{userId}");
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer", TestJwt.Create(DateTimeOffset.UtcNow, subject: "identity-id"));

        return await _client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private async Task<AdminUserDetailResponse> GetUserAsync(Guid userId)
    {
        using HttpResponseMessage response = await GetAsync(userId);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        return (await response.Content.ReadFromJsonAsync<AdminUserDetailResponse>(TestContext.Current.CancellationToken))!;
    }

    private async Task<User> AddCallerAsync(params string[] permissions)
    {
        Role callerRole = await AddRoleAsync("Caller Role", permissions);

        return await AddUserAsync("identity-id", "username", callerRole.Id);
    }

    private async Task<User> AddUserAsync(string identityId, string username, params Guid[] roleIds)
    {
        User user = User.Create(identityId, username, "Display Name", null);

        foreach (Guid roleId in roleIds)
        {
            user.AssignRole(roleId, null, ChocoApiFactory.StartTime);
        }

        await AddAsync(user);

        return user;
    }

    private async Task<Role> AddRoleAsync(string name, params string[] permissions)
    {
        Role role = Role.Create(name, permissions);
        await AddAsync(role);

        return role;
    }

    private async Task<Post> AddPostAsync(User author)
    {
        Post post = Post.Create("content", author.Id);
        await AddAsync(post);

        return post;
    }

    private Task AddAsync(object entity)
    {
        return _factory.ExecuteDbContextAsync(dbContext =>
        {
            dbContext.Add(entity);
            return dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
    }

    private Task SuspendAsync(Guid id, string reason, Guid suspendedBy)
    {
        return _factory.ExecuteDbContextAsync(dbContext =>
        {
            dbContext.Set<User>().Single(user => user.Id == id).Suspend(reason, suspendedBy, _factory.TimeProvider.GetUtcNow());
            return dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
    }

    private Task DeleteUserAsync(Guid id)
    {
        return _factory.ExecuteDbContextAsync(dbContext =>
        {
            dbContext.Remove(dbContext.Set<User>().Single(user => user.Id == id));
            return dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
    }
}
