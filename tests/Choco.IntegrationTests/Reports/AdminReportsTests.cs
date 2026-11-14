using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Choco.Contract.Abstractions;
using Choco.Contract.Reports;
using Choco.Domain.Posts;
using Choco.Domain.Reports;
using Choco.Domain.Roles;
using Choco.Domain.Users;
using Choco.IntegrationTests.Notifications;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Choco.IntegrationTests.Reports;

public sealed class AdminReportsTests : IClassFixture<ChocoApiFactory>, IAsyncLifetime
{
    private const string Path = "/api/v1/admin/reports";

    private readonly ChocoApiFactory _factory;
    private readonly HttpClient _client;

    public AdminReportsTests(ChocoApiFactory factory)
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
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task Member_ReturnsForbidden()
    {
        await NotificationTestData.AddUserAsync(_factory, "identity-id", "username");
        using HttpRequestMessage request = NotificationTestData.CreateRequest(HttpMethod.Get, Path);

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        ProblemDetails problem = (await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken))!;
        problem.Extensions["code"].Should().BeOfType<JsonElement>().Which.GetString().Should().Be("Permission.Denied");
    }

    [Fact]
    public async Task NoToken_ReturnsUnauthorized()
    {
        using HttpRequestMessage request = new(HttpMethod.Get, Path);

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task OpenReport_ListsEveryFieldWithoutAnOutcome()
    {
        await AddAdminAsync("identity-id", "admin");
        User reporter = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "reporter");
        User author = await NotificationTestData.AddUserAsync(_factory, "identity-id-3", "author");
        Post post = await NotificationTestData.AddPostAsync(_factory, author);
        Report report = await AddReportAsync(
            Report.CreateForPost(reporter.Id, post.Id, author.Id, ReportReason.Spam, "details", _factory.TimeProvider.GetUtcNow()).Value);

        OffsetPageResponse<AdminReportResponse> page = await GetPageAsync(Path);

        page.TotalCount.Should().Be(1);
        AdminReportResponse item = page.Items.Should().ContainSingle().Subject;
        item.Id.Should().Be(report.Id);
        item.TargetType.Should().Be("Post");
        item.TargetId.Should().Be(post.Id);
        item.Reason.Should().Be("Spam");
        item.Details.Should().Be("details");
        item.Reporter.Should().Be(new ReportUserResponse(reporter.Id, "reporter"));
        item.CreatedAt.Should().Be(_factory.TimeProvider.GetUtcNow());
        item.Status.Should().Be("Open");
        item.Outcome.Should().BeNull();
        item.Resolver.Should().BeNull();
        item.ResolvedAt.Should().BeNull();
    }

    [Fact]
    public async Task ResolvedReport_ListsTheOutcomeTheResolverAndTheTime()
    {
        User admin = await AddAdminAsync("identity-id", "admin");
        User reporter = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "reporter");
        User target = await NotificationTestData.AddUserAsync(_factory, "identity-id-3", "target");
        Report report = await AddReportAsync(
            Report.CreateForUser(reporter.Id, target.Id, ReportReason.Harassment, null, _factory.TimeProvider.GetUtcNow()).Value);
        _factory.TimeProvider.Advance(TimeSpan.FromHours(1));
        await ResolveAsync(report.Id, ReportOutcome.UserSuspended, admin.Id);

        OffsetPageResponse<AdminReportResponse> page = await GetPageAsync(Path);

        AdminReportResponse item = page.Items.Should().ContainSingle().Subject;
        item.TargetType.Should().Be("User");
        item.TargetId.Should().Be(target.Id);
        item.Details.Should().BeNull();
        item.Status.Should().Be("Resolved");
        item.Outcome.Should().Be("UserSuspended");
        item.Resolver.Should().Be(new ReportUserResponse(admin.Id, "admin"));
        item.ResolvedAt.Should().Be(_factory.TimeProvider.GetUtcNow());
    }

    [Fact]
    public async Task Reports_AreListedNewestFirstInNumberedPages()
    {
        await AddAdminAsync("identity-id", "admin");
        User reporter = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "reporter");
        List<Report> reports = [];
        for (int index = 0; index < 5; index++)
        {
            User target = await NotificationTestData.AddUserAsync(_factory, $"target-identity-{index}", $"target-{index}");
            reports.Add(await AddReportAsync(
                Report.CreateForUser(reporter.Id, target.Id, ReportReason.Spam, null, _factory.TimeProvider.GetUtcNow()).Value));
            _factory.TimeProvider.Advance(TimeSpan.FromMinutes(1));
        }

        OffsetPageResponse<AdminReportResponse> first = await GetPageAsync($"{Path}?pageSize=2");
        OffsetPageResponse<AdminReportResponse> last = await GetPageAsync($"{Path}?pageSize=2&page=3");
        OffsetPageResponse<AdminReportResponse> pastEnd = await GetPageAsync($"{Path}?pageSize=2&page=99");

        first.TotalCount.Should().Be(5);
        first.Items.Select(item => item.Id).Should().Equal(reports[4].Id, reports[3].Id);
        last.Items.Select(item => item.Id).Should().Equal(reports[0].Id);
        pastEnd.Items.Should().BeEmpty();
        pastEnd.TotalCount.Should().Be(5);
    }

    [Fact]
    public async Task TargetReportedMoreThanTenTimes_ListsEveryReportThroughTheTargetIdFilter()
    {
        await AddAdminAsync("identity-id", "admin");
        User author = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "author");
        Post post = await NotificationTestData.AddPostAsync(_factory, author);
        Post otherPost = await NotificationTestData.AddPostAsync(_factory, author);
        List<Guid> expected = [];
        for (int index = 0; index < 12; index++)
        {
            User reporter = await NotificationTestData.AddUserAsync(_factory, $"reporter-identity-{index}", $"reporter-{index}");
            Report report = await AddReportAsync(
                Report.CreateForPost(reporter.Id, post.Id, author.Id, ReportReason.Spam, null, _factory.TimeProvider.GetUtcNow()).Value);
            expected.Add(report.Id);
            _factory.TimeProvider.Advance(TimeSpan.FromMinutes(1));
        }

        User another = await NotificationTestData.AddUserAsync(_factory, "another-identity", "another");
        await AddReportAsync(
            Report.CreateForPost(another.Id, otherPost.Id, author.Id, ReportReason.Spam, null, _factory.TimeProvider.GetUtcNow()).Value);

        OffsetPageResponse<AdminReportResponse> all = await GetPageAsync($"{Path}?targetId={post.Id}&pageSize=50");
        OffsetPageResponse<AdminReportResponse> secondPage = await GetPageAsync($"{Path}?targetId={post.Id}&pageSize=5&page=2");

        all.TotalCount.Should().Be(12);
        all.Items.Select(item => item.Id).Should().BeEquivalentTo(expected);
        secondPage.Items.Should().HaveCount(5);
        secondPage.TotalCount.Should().Be(12);
    }

    [Fact]
    public async Task Filters_NarrowTheListByTargetTypeStatusReasonAndOutcome()
    {
        User admin = await AddAdminAsync("identity-id", "admin");
        User reporter = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "reporter");
        User author = await NotificationTestData.AddUserAsync(_factory, "identity-id-3", "author");
        User target = await NotificationTestData.AddUserAsync(_factory, "identity-id-4", "target");
        Post post = await NotificationTestData.AddPostAsync(_factory, author);
        Report postSpam = await AddReportAsync(
            Report.CreateForPost(reporter.Id, post.Id, author.Id, ReportReason.Spam, null, _factory.TimeProvider.GetUtcNow()).Value);
        Report userHarassment = await AddReportAsync(
            Report.CreateForUser(reporter.Id, target.Id, ReportReason.Harassment, null, _factory.TimeProvider.GetUtcNow()).Value);
        await ResolveAsync(postSpam.Id, ReportOutcome.ContentRemoved, admin.Id);

        (await GetIdsAsync($"{Path}?targetType=Post")).Should().Equal(postSpam.Id);
        (await GetIdsAsync($"{Path}?targetType=User")).Should().Equal(userHarassment.Id);
        (await GetIdsAsync($"{Path}?status=Open")).Should().Equal(userHarassment.Id);
        (await GetIdsAsync($"{Path}?status=Resolved")).Should().Equal(postSpam.Id);
        (await GetIdsAsync($"{Path}?reason=Harassment")).Should().Equal(userHarassment.Id);
        (await GetIdsAsync($"{Path}?outcome=ContentRemoved")).Should().Equal(postSpam.Id);
        (await GetIdsAsync($"{Path}?outcome=Dismissed")).Should().BeEmpty();
        (await GetIdsAsync($"{Path}?targetType=Post&status=Open")).Should().BeEmpty();
    }

    [Fact]
    public async Task ReporterAndResolverWithDeletedAccounts_HaveNullUsernames()
    {
        User admin = await AddAdminAsync("identity-id", "admin");
        User reporter = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "reporter");
        User resolver = await AddAdminAsync("identity-id-3", "resolver");
        User target = await NotificationTestData.AddUserAsync(_factory, "identity-id-4", "target");
        Report report = await AddReportAsync(
            Report.CreateForUser(reporter.Id, target.Id, ReportReason.Spam, null, _factory.TimeProvider.GetUtcNow()).Value);
        await ResolveAsync(report.Id, ReportOutcome.Dismissed, resolver.Id);
        await DeleteUserAsync(reporter.Id);
        await DeleteUserAsync(resolver.Id);

        OffsetPageResponse<AdminReportResponse> page = await GetPageAsync(Path);

        AdminReportResponse item = page.Items.Should().ContainSingle().Subject;
        item.Reporter.Should().Be(new ReportUserResponse(reporter.Id, null));
        item.Resolver.Should().Be(new ReportUserResponse(resolver.Id, null));
        admin.Id.Should().NotBe(resolver.Id);
    }

    [Fact]
    public async Task ReportsAboutADeletedPost_AreStillListed()
    {
        await AddAdminAsync("identity-id", "admin");
        User reporter = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "reporter");
        User author = await NotificationTestData.AddUserAsync(_factory, "identity-id-3", "author");
        Post post = await NotificationTestData.AddPostAsync(_factory, author);
        Report report = await AddReportAsync(
            Report.CreateForPost(reporter.Id, post.Id, author.Id, ReportReason.Spam, null, _factory.TimeProvider.GetUtcNow()).Value);
        await _factory.ExecuteDbContextAsync(async db =>
        {
            db.Remove(await db.Set<Post>().SingleAsync(p => p.Id == post.Id, TestContext.Current.CancellationToken));
            return await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        (await GetIdsAsync(Path)).Should().Equal(report.Id);
    }

    [Theory]
    [InlineData("targetType=Comment", "targetType")]
    [InlineData("targetType=post", "targetType")]
    [InlineData("targetId=00000000-0000-0000-0000-000000000000", "targetId")]
    [InlineData("status=Closed", "status")]
    [InlineData("reason=Rude", "reason")]
    [InlineData("outcome=Banned", "outcome")]
    [InlineData("page=0", "page")]
    [InlineData("pageSize=0", "pageSize")]
    [InlineData("pageSize=51", "pageSize")]
    public async Task InvalidFilter_ReturnsValidationProblemNamingTheField(string query, string field)
    {
        await AddAdminAsync("identity-id", "admin");
        using HttpRequestMessage request = NotificationTestData.CreateRequest(HttpMethod.Get, $"{Path}?{query}");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        ValidationProblemDetails problem = (await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(TestContext.Current.CancellationToken))!;
        problem.Errors.Keys.Should().ContainSingle().Which.Should().Be(field);
    }

    private async Task<List<Guid>> GetIdsAsync(string uri)
    {
        return (await GetPageAsync(uri)).Items.Select(item => item.Id).ToList();
    }

    private async Task<OffsetPageResponse<AdminReportResponse>> GetPageAsync(string uri)
    {
        using HttpRequestMessage request = NotificationTestData.CreateRequest(HttpMethod.Get, uri);
        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        return (await response.Content.ReadFromJsonAsync<OffsetPageResponse<AdminReportResponse>>(TestContext.Current.CancellationToken))!;
    }

    private async Task<User> AddAdminAsync(string identityId, string username)
    {
        Guid adminRoleId = await _factory.ExecuteDbContextAsync(db => db.Set<Role>()
            .Where(role => role.SystemKey == SystemRoles.Admin)
            .Select(role => role.Id)
            .SingleAsync(TestContext.Current.CancellationToken));
        User user = User.Create(identityId, username, "Display Name", null);
        user.AssignRole(adminRoleId, null, ChocoApiFactory.StartTime);
        await _factory.ExecuteDbContextAsync(db =>
        {
            db.Add(user);
            return db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        return user;
    }

    private async Task<Report> AddReportAsync(Report report)
    {
        await _factory.ExecuteDbContextAsync(db =>
        {
            db.Add(report);
            return db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        return report;
    }

    private async Task ResolveAsync(Guid reportId, ReportOutcome outcome, Guid resolverId)
    {
        await _factory.ExecuteDbContextAsync(async db =>
        {
            Report report = await db.Set<Report>().SingleAsync(r => r.Id == reportId, TestContext.Current.CancellationToken);
            report.Resolve(outcome, resolverId, _factory.TimeProvider.GetUtcNow());
            return await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
    }

    private async Task DeleteUserAsync(Guid userId)
    {
        await _factory.ExecuteDbContextAsync(async db =>
        {
            db.Remove(await db.Set<User>().SingleAsync(user => user.Id == userId, TestContext.Current.CancellationToken));
            return await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
    }
}
