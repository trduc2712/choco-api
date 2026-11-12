using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using Choco.Domain.Comments;
using Choco.Domain.Posts;
using Choco.Domain.Reports;
using Choco.Domain.Roles;
using Choco.Domain.Users;
using Choco.IntegrationTests.Authentication;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Choco.IntegrationTests.Roles;

public sealed class ModeratorRoleTests : IClassFixture<ChocoApiFactory>, IAsyncLifetime
{
    private const string Id = "00000000-0000-0000-0000-000000000001";

    private readonly ChocoApiFactory _factory;
    private readonly HttpClient _client;

    public ModeratorRoleTests(ChocoApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public static TheoryData<string> AllowedReads => new()
    {
        "/api/v1/reports",
        "/api/v1/admin/users",
        "/api/v1/admin/posts",
        "/api/v1/admin/comments",
        "/api/v1/admin/counts"
    };

    public static TheoryData<string, string, string?> RefusedRequests => new()
    {
        { "GET", "/api/v1/admin/audit-logs", null },
        { "GET", "/api/v1/admin/feedback", null },
        { "PATCH", $"/api/v1/admin/feedback/{Id}", """{"status":"resolved"}""" },
        { "GET", "/api/v1/admin/roles", null },
        { "GET", "/api/v1/admin/permissions", null },
        { "POST", "/api/v1/admin/roles", """{"name":"Role","permissions":[]}""" },
        { "PUT", $"/api/v1/admin/roles/{Id}", """{"name":"Role","permissions":[]}""" },
        { "DELETE", $"/api/v1/admin/roles/{Id}", null },
        { "PUT", $"/api/v1/users/{Id}/roles/{Id}", null },
        { "DELETE", $"/api/v1/users/{Id}/roles/{Id}", null }
    };

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
    public async Task ModeratorRole_ExistsAndIsNotTheDefaultRole()
    {
        Role role = await GetRoleAsync(SystemRoles.Moderator);

        role.Name.Should().Be("Moderator");
        role.IsDefault.Should().BeFalse();
    }

    [Fact]
    public async Task Me_OfAModerator_ListsExactlyThePermissionsOfModeration()
    {
        await AddModeratorAsync();

        using HttpResponseMessage response = await SendAsync("identity-id", HttpMethod.Get, "/api/v1/me", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        body.GetProperty("permissions").EnumerateArray().Select(permission => permission.GetString()).Should().Equal(
            Permission.DeleteAnyComment,
            Permission.ReadAnyContent,
            Permission.DeleteAnyPost,
            Permission.HandleReports,
            Permission.ReadAnyUser,
            Permission.SuspendUsers);
    }

    [Theory]
    [MemberData(nameof(AllowedReads))]
    public async Task Moderator_CanRead(string path)
    {
        await AddModeratorAsync();

        using HttpResponseMessage response = await SendAsync("identity-id", HttpMethod.Get, path, null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [MemberData(nameof(RefusedRequests))]
    public async Task Moderator_IsRefusedWhatIsNotModeration(string method, string path, string? body)
    {
        await AddModeratorAsync();

        using HttpResponseMessage response = await SendAsync("identity-id", new HttpMethod(method), path, body);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        ProblemDetails problem = (await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken))!;
        problem.Extensions["code"].Should().BeOfType<JsonElement>().Which.GetString().Should().Be("Permission.Denied");
    }

    [Fact]
    public async Task Moderator_CanResolveAReport()
    {
        await AddModeratorAsync();
        User author = await AddUserAsync("identity-id-2", "username-2");
        await AddUserAsync("identity-id-3", "username-3");
        Post post = await AddPostAsync(author);
        using HttpResponseMessage reported = await SendAsync(
            "identity-id-3", HttpMethod.Post, $"/api/v1/posts/{post.Id}/reports", """{"reason":"Spam","details":"details"}""");
        Report report = await _factory.ExecuteDbContextAsync(dbContext => dbContext.Set<Report>()
            .AsNoTracking()
            .SingleAsync(TestContext.Current.CancellationToken));

        using HttpResponseMessage resolved = await SendAsync(
            "identity-id", HttpMethod.Put, $"/api/v1/reports/{report.Id}/resolution", """{"outcome":"Dismissed"}""");

        reported.IsSuccessStatusCode.Should().BeTrue();
        resolved.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Moderator_CanDeleteAnotherUsersPostAndComment()
    {
        await AddModeratorAsync();
        User author = await AddUserAsync("identity-id-2", "username-2");
        Post post = await AddPostAsync(author);
        Post otherPost = await AddPostAsync(author);
        Comment comment = Comment.Create(otherPost.Id, author.Id, "comment");
        await _factory.ExecuteDbContextAsync(dbContext =>
        {
            dbContext.Add(comment);
            return dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        using HttpResponseMessage postResponse = await SendAsync("identity-id", HttpMethod.Delete, $"/api/v1/posts/{post.Id}", null);
        using HttpResponseMessage commentResponse = await SendAsync("identity-id", HttpMethod.Delete, $"/api/v1/comments/{comment.Id}", null);

        postResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        commentResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Moderator_CanSuspendAMemberAndLiftTheSuspension()
    {
        await AddModeratorAsync();
        User member = await AddUserAsync("identity-id-2", "username-2");

        using HttpResponseMessage suspend = await SendAsync(
            "identity-id", HttpMethod.Put, $"/api/v1/admin/users/{member.Id}/suspension", """{"reason":"Repeated spam"}""");
        using HttpResponseMessage lift = await SendAsync(
            "identity-id", HttpMethod.Delete, $"/api/v1/admin/users/{member.Id}/suspension", null);

        suspend.StatusCode.Should().Be(HttpStatusCode.NoContent);
        lift.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Moderator_CannotSuspendAnAdministrator()
    {
        await AddModeratorAsync();
        User administrator = await AddUserAsync("identity-id-2", "username-2", (await GetRoleAsync(SystemRoles.Admin)).Id);

        using HttpResponseMessage response = await SendAsync(
            "identity-id", HttpMethod.Put, $"/api/v1/admin/users/{administrator.Id}/suspension", """{"reason":"reason"}""");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        ProblemDetails problem = (await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken))!;
        problem.Extensions["code"].Should().BeOfType<JsonElement>().Which.GetString().Should().Be("Permission.Denied");
    }

    [Fact]
    public async Task Moderator_CannotAssignRoles()
    {
        await AddModeratorAsync();
        User member = await AddUserAsync("identity-id-2", "username-2");
        Role moderator = await GetRoleAsync(SystemRoles.Moderator);

        using HttpResponseMessage response = await SendAsync(
            "identity-id", HttpMethod.Put, $"/api/v1/users/{member.Id}/roles/{moderator.Id}", null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private async Task<HttpResponseMessage> SendAsync(string identityId, HttpMethod method, string path, string? body)
    {
        using HttpRequestMessage request = new(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer", TestJwt.Create(DateTimeOffset.UtcNow, subject: identityId));

        if (body is not null)
        {
            request.Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
        }

        return await _client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private async Task AddModeratorAsync()
    {
        Role moderator = await GetRoleAsync(SystemRoles.Moderator);
        await AddUserAsync("identity-id", "username", moderator.Id);
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

    private async Task<Post> AddPostAsync(User author)
    {
        Post post = Post.Create("content", author.Id);

        await _factory.ExecuteDbContextAsync(dbContext =>
        {
            dbContext.Add(post);
            return dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        return post;
    }

    private Task<Role> GetRoleAsync(string systemKey)
    {
        return _factory.ExecuteDbContextAsync(dbContext => dbContext.Set<Role>()
            .AsNoTracking()
            .SingleAsync(role => role.SystemKey == systemKey, TestContext.Current.CancellationToken));
    }
}
