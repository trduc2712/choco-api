using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using Choco.Domain.Posts;
using Choco.Domain.Reports;
using Choco.Domain.Roles;
using Choco.Domain.Users;
using Choco.IntegrationTests.Authentication;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Choco.IntegrationTests.Reports;

public sealed class ResolveReportOutcomeTests : IClassFixture<ChocoApiFactory>, IAsyncLifetime
{
    private readonly ChocoApiFactory _factory;
    private readonly HttpClient _client;

    public ResolveReportOutcomeTests(ChocoApiFactory factory)
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
    public async Task ContentRemoved_DeletesThePostAndResolvesTheReport()
    {
        User moderator = await AddModeratorAsync();
        User author = await AddUserAsync("identity-id-2", "username-2");
        User reporter = await AddUserAsync("identity-id-3", "username-3");
        Post post = await AddPostAsync(author);
        Report report = await AddPostReportAsync(reporter, post, author);

        using HttpResponseMessage response = await ResolveAsync(report.Id, "ContentRemoved");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await GetPostAsync(post.Id)).IsDeleted.Should().BeTrue();
        Report stored = await GetReportAsync(report.Id);
        stored.Status.Should().Be(ReportStatus.Resolved);
        stored.Outcome.Should().Be(ReportOutcome.ContentRemoved);
        stored.ResolvedById.Should().Be(moderator.Id);
    }

    [Fact]
    public async Task ContentRemoved_ResolvesEveryOpenReportAboutTheSamePostAndNoOther()
    {
        await AddModeratorAsync();
        User author = await AddUserAsync("identity-id-2", "username-2");
        User firstReporter = await AddUserAsync("identity-id-3", "username-3");
        User secondReporter = await AddUserAsync("identity-id-4", "username-4");
        Post post = await AddPostAsync(author);
        Post otherPost = await AddPostAsync(author);
        Report first = await AddPostReportAsync(firstReporter, post, author);
        Report second = await AddPostReportAsync(secondReporter, post, author);
        Report other = await AddPostReportAsync(firstReporter, otherPost, author);

        using HttpResponseMessage response = await ResolveAsync(first.Id, "ContentRemoved");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        Report storedSecond = await GetReportAsync(second.Id);
        storedSecond.Status.Should().Be(ReportStatus.Resolved);
        storedSecond.Outcome.Should().Be(ReportOutcome.ContentRemoved);
        (await GetReportAsync(other.Id)).Status.Should().Be(ReportStatus.Open);
        (await GetPostAsync(otherPost.Id)).IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task ContentRemoved_ForAReportAboutAUser_ReturnsBadRequestAndKeepsTheReportOpen()
    {
        await AddModeratorAsync();
        User target = await AddUserAsync("identity-id-2", "username-2");
        User reporter = await AddUserAsync("identity-id-3", "username-3");
        Report report = await AddUserReportAsync(reporter, target);

        using HttpResponseMessage response = await ResolveAsync(report.Id, "ContentRemoved");

        await ShouldBeProblemAsync(response, HttpStatusCode.BadRequest, "Report.OutcomeNotApplicable");
        (await GetReportAsync(report.Id)).Status.Should().Be(ReportStatus.Open);
    }

    [Fact]
    public async Task ContentRemoved_WhenThePostIsAlreadyDeleted_StillResolvesTheReport()
    {
        await AddModeratorAsync();
        User author = await AddUserAsync("identity-id-2", "username-2");
        User reporter = await AddUserAsync("identity-id-3", "username-3");
        Post post = await AddPostAsync(author);
        Report report = await AddPostReportAsync(reporter, post, author);
        await DeletePostAsync(post.Id);

        using HttpResponseMessage response = await ResolveAsync(report.Id, "ContentRemoved");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await GetReportAsync(report.Id)).Outcome.Should().Be(ReportOutcome.ContentRemoved);
    }

    [Fact]
    public async Task ContentRemoved_WithoutThePermissionToDeleteAnyPost_ReturnsForbiddenAndChangesNothing()
    {
        await AddCallerAsync(Permission.HandleReports);
        User author = await AddUserAsync("identity-id-2", "username-2");
        User reporter = await AddUserAsync("identity-id-3", "username-3");
        Post post = await AddPostAsync(author);
        Report report = await AddPostReportAsync(reporter, post, author);

        using HttpResponseMessage response = await ResolveAsync(report.Id, "ContentRemoved");

        await ShouldBeProblemAsync(response, HttpStatusCode.Forbidden, "Permission.Denied");
        (await GetPostAsync(post.Id)).IsDeleted.Should().BeFalse();
        (await GetReportAsync(report.Id)).Status.Should().Be(ReportStatus.Open);
    }

    [Fact]
    public async Task UserSuspended_ForAReportAboutAPost_SuspendsTheAuthorWithTheReasonOfTheReportAndKeepsThePost()
    {
        User moderator = await AddModeratorAsync();
        User author = await AddUserAsync("identity-id-2", "username-2");
        User reporter = await AddUserAsync("identity-id-3", "username-3");
        Post post = await AddPostAsync(author);
        Report report = await AddPostReportAsync(reporter, post, author, ReportReason.HateSpeech, "details written by the reporter");

        using HttpResponseMessage response = await ResolveAsync(report.Id, "UserSuspended");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        User stored = await GetUserAsync(author.Id);
        stored.IsSuspended.Should().BeTrue();
        stored.SuspensionReason.Should().Be("Reported for hate speech");
        stored.SuspendedBy.Should().Be(moderator.Id);
        (await GetPostAsync(post.Id)).IsDeleted.Should().BeFalse();
        (await GetReportAsync(report.Id)).Outcome.Should().Be(ReportOutcome.UserSuspended);
    }

    [Fact]
    public async Task UserSuspended_ForAReportAboutAUser_SuspendsThemAndResolvesEveryOpenReportAboutThem()
    {
        await AddModeratorAsync();
        User target = await AddUserAsync("identity-id-2", "username-2");
        User firstReporter = await AddUserAsync("identity-id-3", "username-3");
        User secondReporter = await AddUserAsync("identity-id-4", "username-4");
        Report first = await AddUserReportAsync(firstReporter, target, ReportReason.Harassment);
        Report second = await AddUserReportAsync(secondReporter, target, ReportReason.Spam);

        using HttpResponseMessage response = await ResolveAsync(first.Id, "UserSuspended");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await GetUserAsync(target.Id)).SuspensionReason.Should().Be("Reported for harassment");
        Report storedSecond = await GetReportAsync(second.Id);
        storedSecond.Status.Should().Be(ReportStatus.Resolved);
        storedSecond.Outcome.Should().Be(ReportOutcome.UserSuspended);
    }

    [Fact]
    public async Task UserSuspended_WhenTheUserIsAlreadySuspended_KeepsTheFirstSuspensionAndResolvesTheReport()
    {
        await AddModeratorAsync();
        User target = await AddUserAsync("identity-id-2", "username-2");
        User reporter = await AddUserAsync("identity-id-3", "username-3");
        Report report = await AddUserReportAsync(reporter, target, ReportReason.Spam);
        await SuspendDirectlyAsync(target.Id, "first reason");

        using HttpResponseMessage response = await ResolveAsync(report.Id, "UserSuspended");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await GetUserAsync(target.Id)).SuspensionReason.Should().Be("first reason");
        (await GetReportAsync(report.Id)).Outcome.Should().Be(ReportOutcome.UserSuspended);
    }

    [Fact]
    public async Task UserSuspended_WithoutThePermissionToSuspendUsers_ReturnsForbiddenAndChangesNothing()
    {
        await AddCallerAsync(Permission.HandleReports);
        User target = await AddUserAsync("identity-id-2", "username-2");
        User reporter = await AddUserAsync("identity-id-3", "username-3");
        Report report = await AddUserReportAsync(reporter, target);

        using HttpResponseMessage response = await ResolveAsync(report.Id, "UserSuspended");

        await ShouldBeProblemAsync(response, HttpStatusCode.Forbidden, "Permission.Denied");
        (await GetUserAsync(target.Id)).IsSuspended.Should().BeFalse();
        (await GetReportAsync(report.Id)).Status.Should().Be(ReportStatus.Open);
    }

    [Fact]
    public async Task UserSuspended_ForAUserWhosePermissionsTheCallerLacks_ReturnsForbiddenAndChangesNothing()
    {
        await AddModeratorAsync();
        User administrator = await AddUserAsync("identity-id-2", "username-2", (await GetSystemRoleAsync(SystemRoles.Admin)).Id);
        User reporter = await AddUserAsync("identity-id-3", "username-3");
        Report report = await AddUserReportAsync(reporter, administrator);

        using HttpResponseMessage response = await ResolveAsync(report.Id, "UserSuspended");

        await ShouldBeProblemAsync(response, HttpStatusCode.Forbidden, "Permission.Denied");
        (await GetUserAsync(administrator.Id)).IsSuspended.Should().BeFalse();
        (await GetReportAsync(report.Id)).Status.Should().Be(ReportStatus.Open);
    }

    [Fact]
    public async Task UserSuspended_ForTheLastAdministratorWhoIsNotSuspended_ReturnsConflictAndKeepsTheReportOpen()
    {
        await AddCallerAsync([.. Permission.All]);
        User administrator = await AddUserAsync("identity-id-2", "username-2", (await GetSystemRoleAsync(SystemRoles.Admin)).Id);
        User reporter = await AddUserAsync("identity-id-3", "username-3");
        Report report = await AddUserReportAsync(reporter, administrator);

        using HttpResponseMessage response = await ResolveAsync(report.Id, "UserSuspended");

        await ShouldBeProblemAsync(response, HttpStatusCode.Conflict, "User.LastAdministrator");
        (await GetReportAsync(report.Id)).Status.Should().Be(ReportStatus.Open);
    }

    [Fact]
    public async Task UserSuspended_ForTheResolverThemselves_ReturnsBadRequestAndKeepsTheReportOpen()
    {
        User moderator = await AddModeratorAsync();
        User reporter = await AddUserAsync("identity-id-3", "username-3");
        Report report = await AddUserReportAsync(reporter, moderator);

        using HttpResponseMessage response = await ResolveAsync(report.Id, "UserSuspended");

        await ShouldBeProblemAsync(response, HttpStatusCode.BadRequest, "User.CannotSuspendSelf");
        (await GetUserAsync(moderator.Id)).IsSuspended.Should().BeFalse();
        (await GetReportAsync(report.Id)).Status.Should().Be(ReportStatus.Open);
    }

    [Fact]
    public async Task UserSuspended_ForAUserWhoDeletedTheirAccount_ReturnsNotFoundAndKeepsTheReportOpen()
    {
        await AddModeratorAsync();
        User target = await AddUserAsync("identity-id-2", "username-2");
        User reporter = await AddUserAsync("identity-id-3", "username-3");
        Report report = await AddUserReportAsync(reporter, target);
        await DeleteUserAsync(target.Id);

        using HttpResponseMessage response = await ResolveAsync(report.Id, "UserSuspended");

        await ShouldBeProblemAsync(response, HttpStatusCode.NotFound, "User.NotFound");
        (await GetReportAsync(report.Id)).Status.Should().Be(ReportStatus.Open);
    }

    [Fact]
    public async Task Dismissed_ResolvesEveryOpenReportAboutTheTargetAndChangesNothingElse()
    {
        await AddModeratorAsync();
        User author = await AddUserAsync("identity-id-2", "username-2");
        User firstReporter = await AddUserAsync("identity-id-3", "username-3");
        User secondReporter = await AddUserAsync("identity-id-4", "username-4");
        Post post = await AddPostAsync(author);
        Report first = await AddPostReportAsync(firstReporter, post, author);
        Report second = await AddPostReportAsync(secondReporter, post, author);

        using HttpResponseMessage response = await ResolveAsync(first.Id, "Dismissed");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await GetReportAsync(second.Id)).Outcome.Should().Be(ReportOutcome.Dismissed);
        (await GetPostAsync(post.Id)).IsDeleted.Should().BeFalse();
        (await GetUserAsync(author.Id)).IsSuspended.Should().BeFalse();
    }

    [Fact]
    public async Task AnAlreadyResolvedReport_ResolvedAgainWithAnotherOutcome_DoesNotAct()
    {
        await AddModeratorAsync();
        User author = await AddUserAsync("identity-id-2", "username-2");
        User reporter = await AddUserAsync("identity-id-3", "username-3");
        Post post = await AddPostAsync(author);
        Report report = await AddPostReportAsync(reporter, post, author);
        using HttpResponseMessage first = await ResolveAsync(report.Id, "Dismissed");

        using HttpResponseMessage second = await ResolveAsync(report.Id, "ContentRemoved");

        first.StatusCode.Should().Be(HttpStatusCode.NoContent);
        second.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await GetReportAsync(report.Id)).Outcome.Should().Be(ReportOutcome.Dismissed);
        (await GetPostAsync(post.Id)).IsDeleted.Should().BeFalse();
    }

    [Theory]
    [InlineData("ActionTaken")]
    [InlineData("dismissed")]
    public async Task OutcomeThatDoesNotExist_ReturnsValidationProblem(string outcome)
    {
        await AddModeratorAsync();
        User target = await AddUserAsync("identity-id-2", "username-2");
        User reporter = await AddUserAsync("identity-id-3", "username-3");
        Report report = await AddUserReportAsync(reporter, target);

        using HttpResponseMessage response = await ResolveAsync(report.Id, outcome);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        ValidationProblemDetails problem = (await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(TestContext.Current.CancellationToken))!;
        problem.Errors.Keys.Should().ContainSingle().Which.Should().BeEquivalentTo("outcome");
        (await GetReportAsync(report.Id)).Status.Should().Be(ReportStatus.Open);
    }

    private static async Task ShouldBeProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        response.StatusCode.Should().Be(status);
        ProblemDetails problem = (await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken))!;
        problem.Extensions["code"].Should().BeOfType<JsonElement>().Which.GetString().Should().Be(code);
    }

    private async Task<HttpResponseMessage> ResolveAsync(Guid reportId, string outcome)
    {
        using HttpRequestMessage request = new(HttpMethod.Put, $"/api/v1/reports/{reportId}/resolution")
        {
            Content = JsonContent.Create(new { outcome })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer", TestJwt.Create(DateTimeOffset.UtcNow, subject: "identity-id"));

        return await _client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private async Task<User> AddModeratorAsync()
    {
        Role moderator = await GetSystemRoleAsync(SystemRoles.Moderator);

        return await AddUserAsync("identity-id", "username", moderator.Id);
    }

    private async Task<User> AddCallerAsync(params string[] permissions)
    {
        Role role = Role.Create("Caller Role", permissions);
        await AddAsync(role);

        return await AddUserAsync("identity-id", "username", role.Id);
    }

    private async Task<User> AddUserAsync(string identityId, string username, Guid? roleId = null)
    {
        User user = User.Create(identityId, username, "Display Name", null);

        if (roleId is not null)
        {
            user.AssignRole(roleId.Value, null, ChocoApiFactory.StartTime);
        }

        await AddAsync(user);

        return user;
    }

    private async Task<Post> AddPostAsync(User author)
    {
        Post post = Post.Create("content", author.Id);
        await AddAsync(post);

        return post;
    }

    private async Task<Report> AddPostReportAsync(
        User reporter, Post post, User author, ReportReason reason = ReportReason.Spam, string? details = null)
    {
        _factory.TimeProvider.Advance(TimeSpan.FromSeconds(1));
        Report report = Report.CreateForPost(reporter.Id, post.Id, author.Id, reason, details, _factory.TimeProvider.GetUtcNow()).Value;
        await AddAsync(report);

        return report;
    }

    private async Task<Report> AddUserReportAsync(User reporter, User target, ReportReason reason = ReportReason.Spam)
    {
        _factory.TimeProvider.Advance(TimeSpan.FromSeconds(1));
        Report report = Report.CreateForUser(reporter.Id, target.Id, reason, null, _factory.TimeProvider.GetUtcNow()).Value;
        await AddAsync(report);

        return report;
    }

    private Task AddAsync(object entity)
    {
        return _factory.ExecuteDbContextAsync(dbContext =>
        {
            dbContext.Add(entity);
            return dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
    }

    private Task DeletePostAsync(Guid id)
    {
        return _factory.ExecuteDbContextAsync(dbContext =>
        {
            dbContext.Remove(dbContext.Set<Post>().Single(post => post.Id == id));
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

    private Task SuspendDirectlyAsync(Guid id, string reason)
    {
        return _factory.ExecuteDbContextAsync(dbContext =>
        {
            dbContext.Set<User>().Single(user => user.Id == id).Suspend(reason, id, ChocoApiFactory.StartTime);
            return dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
    }

    private Task<Report> GetReportAsync(Guid id)
    {
        return _factory.ExecuteDbContextAsync(dbContext => dbContext.Set<Report>()
            .AsNoTracking()
            .SingleAsync(report => report.Id == id, TestContext.Current.CancellationToken));
    }

    private Task<Post> GetPostAsync(Guid id)
    {
        return _factory.ExecuteDbContextAsync(dbContext => dbContext.Set<Post>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync(post => post.Id == id, TestContext.Current.CancellationToken));
    }

    private Task<User> GetUserAsync(Guid id)
    {
        return _factory.ExecuteDbContextAsync(dbContext => dbContext.Set<User>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync(user => user.Id == id, TestContext.Current.CancellationToken));
    }

    private Task<Role> GetSystemRoleAsync(string systemKey)
    {
        return _factory.ExecuteDbContextAsync(dbContext => dbContext.Set<Role>()
            .AsNoTracking()
            .SingleAsync(role => role.SystemKey == systemKey, TestContext.Current.CancellationToken));
    }
}
