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

public sealed class ReportTests : IClassFixture<ChocoApiFactory>, IAsyncLifetime
{
    private static readonly Guid UnknownId = new("00000000-0000-0000-0000-000000000001");
    private readonly ChocoApiFactory _factory;
    private readonly HttpClient _client;

    public ReportTests(ChocoApiFactory factory)
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
    public async Task ReportingAPost_CreatesAnOpenReport()
    {
        User caller = await NotificationTestData.AddUserAsync(_factory, "identity-id", "username");
        User author = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "username-2");
        Post post = await NotificationTestData.AddPostAsync(_factory, author);
        using HttpRequestMessage request = PostRequest($"/api/v1/posts/{post.Id}/reports", new { reason = "Spam", details = "details" });

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        Report report = (await GetReportsAsync()).Should().ContainSingle().Subject;
        report.ReporterId.Should().Be(caller.Id);
        report.TargetType.Should().Be(ReportTargetType.Post);
        report.TargetPostId.Should().Be(post.Id);
        report.TargetUserId.Should().BeNull();
        report.Reason.Should().Be(ReportReason.Spam);
        report.Details.Should().Be("details");
        report.CreatedAt.Should().Be(_factory.TimeProvider.GetUtcNow());
        report.Status.Should().Be(ReportStatus.Open);
    }

    [Fact]
    public async Task ReportingAUser_CreatesAnOpenReport()
    {
        User caller = await NotificationTestData.AddUserAsync(_factory, "identity-id", "username");
        User target = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "username-2");
        using HttpRequestMessage request = PostRequest($"/api/v1/users/{target.Id}/reports", new { reason = "Harassment" });

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        Report report = (await GetReportsAsync()).Should().ContainSingle().Subject;
        report.ReporterId.Should().Be(caller.Id);
        report.TargetType.Should().Be(ReportTargetType.User);
        report.TargetPostId.Should().BeNull();
        report.TargetUserId.Should().Be(target.Id);
        report.Reason.Should().Be(ReportReason.Harassment);
        report.Details.Should().BeNull();
    }

    [Theory]
    [InlineData("post")]
    [InlineData("user")]
    public async Task ReportingTheSameTargetTwice_KeepsOneOpenReport(string kind)
    {
        await NotificationTestData.AddUserAsync(_factory, "identity-id", "username");
        User author = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "username-2");
        Post post = await NotificationTestData.AddPostAsync(_factory, author);
        string path = kind == "post" ? $"/api/v1/posts/{post.Id}/reports" : $"/api/v1/users/{author.Id}/reports";
        using HttpRequestMessage first = PostRequest(path, new { reason = "Spam" });
        using HttpRequestMessage second = PostRequest(path, new { reason = "Violence" });

        using HttpResponseMessage firstResponse = await _client.SendAsync(first, TestContext.Current.CancellationToken);
        using HttpResponseMessage secondResponse = await _client.SendAsync(second, TestContext.Current.CancellationToken);

        firstResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        secondResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        (await GetReportsAsync()).Should().ContainSingle().Which.Reason.Should().Be(ReportReason.Spam);
    }

    [Fact]
    public async Task ReportingAfterTheFirstReportIsResolved_CreatesANewOpenReport()
    {
        await AddAdminAsync("identity-id", "username");
        User author = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "username-2");
        string path = $"/api/v1/users/{author.Id}/reports";
        using HttpRequestMessage first = PostRequest(path, new { reason = "Spam" });
        using HttpResponseMessage firstResponse = await _client.SendAsync(first, TestContext.Current.CancellationToken);
        Guid firstId = (await GetReportsAsync()).Single().Id;
        using HttpRequestMessage resolve = PutRequest($"/api/v1/reports/{firstId}/resolution", new { outcome = "Dismissed" });
        using HttpResponseMessage resolveResponse = await _client.SendAsync(resolve, TestContext.Current.CancellationToken);
        using HttpRequestMessage second = PostRequest(path, new { reason = "Spam" });

        using HttpResponseMessage secondResponse = await _client.SendAsync(second, TestContext.Current.CancellationToken);

        firstResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        resolveResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        secondResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        List<Report> reports = await GetReportsAsync();
        reports.Should().HaveCount(2);
        reports.Count(report => report.Status == ReportStatus.Open).Should().Be(1);
    }

    [Fact]
    public async Task ReportingYourOwnPost_ReturnsBadRequest()
    {
        User caller = await NotificationTestData.AddUserAsync(_factory, "identity-id", "username");
        Post post = await NotificationTestData.AddPostAsync(_factory, caller);
        using HttpRequestMessage request = PostRequest($"/api/v1/posts/{post.Id}/reports", new { reason = "Spam" });

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "Report.SelfReport");
        (await GetReportsAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task ReportingYourself_ReturnsBadRequest()
    {
        User caller = await NotificationTestData.AddUserAsync(_factory, "identity-id", "username");
        using HttpRequestMessage request = PostRequest($"/api/v1/users/{caller.Id}/reports", new { reason = "Spam" });

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "Report.SelfReport");
        (await GetReportsAsync()).Should().BeEmpty();
    }

    [Theory]
    [InlineData("posts")]
    [InlineData("users")]
    public async Task ReportingAnUnknownTarget_ReturnsNotFound(string resource)
    {
        await NotificationTestData.AddUserAsync(_factory, "identity-id", "username");
        using HttpRequestMessage request = PostRequest($"/api/v1/{resource}/{UnknownId}/reports", new { reason = "Spam" });

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await AssertProblemAsync(
            response, HttpStatusCode.NotFound, resource == "posts" ? "Post.NotFound" : "User.NotFound");
    }

    [Fact]
    public async Task ReportingADeletedPost_ReturnsNotFound()
    {
        User caller = await NotificationTestData.AddUserAsync(_factory, "identity-id", "username");
        Post post = await NotificationTestData.AddPostAsync(_factory, caller);
        using HttpRequestMessage delete = NotificationTestData.CreateRequest(HttpMethod.Delete, $"/api/v1/posts/{post.Id}");
        using HttpResponseMessage deleteResponse = await _client.SendAsync(delete, TestContext.Current.CancellationToken);
        using HttpRequestMessage request = PostRequest($"/api/v1/posts/{post.Id}/reports", new { reason = "Spam" });

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await AssertProblemAsync(response, HttpStatusCode.NotFound, "Post.NotFound");
    }

    [Theory]
    [InlineData("posts")]
    [InlineData("users")]
    public async Task ReportingWithAnUnknownReason_ReturnsValidationProblem(string resource)
    {
        await NotificationTestData.AddUserAsync(_factory, "identity-id", "username");
        using HttpRequestMessage request = PostRequest($"/api/v1/{resource}/{UnknownId}/reports", new { reason = "Unknown" });

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        ValidationProblemDetails problem = (await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(TestContext.Current.CancellationToken))!;
        problem.Errors.Keys.Should().ContainSingle().Which.Should().Be("reason");
    }

    [Theory]
    [InlineData("POST", "/api/v1/posts/00000000-0000-0000-0000-000000000001/reports")]
    [InlineData("POST", "/api/v1/users/00000000-0000-0000-0000-000000000001/reports")]
    [InlineData("GET", "/api/v1/reports")]
    [InlineData("PUT", "/api/v1/reports/00000000-0000-0000-0000-000000000001/resolution")]
    public async Task NoToken_ReturnsUnauthorized(string method, string path)
    {
        using HttpRequestMessage request = new(new HttpMethod(method), path);

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ListingReports_ReturnsOpenReportsOldestFirstByPage()
    {
        User admin = await AddAdminAsync("identity-id", "username");
        User reporter = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "username-2");
        User target = await NotificationTestData.AddUserAsync(_factory, "identity-id-3", "username-3");
        Post post = await NotificationTestData.AddPostAsync(_factory, target);
        Report first = await AddReportForUserAsync(reporter, target, ReportReason.Spam);
        _factory.TimeProvider.Advance(TimeSpan.FromHours(1));
        Report second = await AddReportForPostAsync(reporter, post, ReportReason.Violence);
        _factory.TimeProvider.Advance(TimeSpan.FromHours(1));
        Report resolved = await AddReportForUserAsync(admin, target, ReportReason.Other);
        await ResolveAsync(resolved.Id);

        OffsetPageResponse<ReportResponse> firstPage = await GetPageAsync("/api/v1/reports?pageSize=1");
        OffsetPageResponse<ReportResponse> secondPage = await GetPageAsync("/api/v1/reports?pageSize=1&page=2");

        ReportResponse firstItem = firstPage.Items.Should().ContainSingle().Subject;
        firstItem.Id.Should().Be(first.Id);
        firstItem.TargetType.Should().Be("User");
        firstItem.TargetUserId.Should().Be(target.Id);
        firstItem.TargetPostId.Should().BeNull();
        firstItem.Reason.Should().Be("Spam");
        firstItem.ReporterId.Should().Be(reporter.Id);
        firstItem.ReporterUsername.Should().Be("username-2");
        firstItem.CreatedAt.Should().Be(first.CreatedAt);
        firstPage.Page.Should().Be(1);
        firstPage.PageSize.Should().Be(1);
        firstPage.TotalCount.Should().Be(2);
        ReportResponse secondItem = secondPage.Items.Should().ContainSingle().Subject;
        secondItem.Id.Should().Be(second.Id);
        secondItem.TargetType.Should().Be("Post");
        secondItem.TargetPostId.Should().Be(post.Id);
        secondPage.Page.Should().Be(2);
        secondPage.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task ListingReports_WithoutQuery_UsesTheFirstPageOfTwentyAndCamelCaseNames()
    {
        await AddAdminAsync("identity-id", "username");
        User reporter = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "username-2");
        User target = await NotificationTestData.AddUserAsync(_factory, "identity-id-3", "username-3");
        await AddReportForUserAsync(reporter, target, ReportReason.Spam);
        using HttpRequestMessage request = NotificationTestData.CreateRequest(HttpMethod.Get, "/api/v1/reports");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        body.EnumerateObject().Select(property => property.Name).Should().BeEquivalentTo(
            "items", "page", "pageSize", "totalCount");
        body.GetProperty("page").GetInt32().Should().Be(1);
        body.GetProperty("pageSize").GetInt32().Should().Be(20);
        body.GetProperty("totalCount").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task ListingReports_ReturnsTheMiddleAndLastPagesAndAnEmptyPagePastTheEnd()
    {
        await AddAdminAsync("identity-id", "username");
        User reporter = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "username-2");
        List<Report> reports = [];
        for (int index = 0; index < 5; index++)
        {
            User target = await NotificationTestData.AddUserAsync(_factory, $"identity-id-target-{index}", $"username-target-{index}");
            reports.Add(await AddReportForUserAsync(reporter, target, ReportReason.Spam));
            _factory.TimeProvider.Advance(TimeSpan.FromHours(1));
        }

        OffsetPageResponse<ReportResponse> middle = await GetPageAsync("/api/v1/reports?pageSize=2&page=2");
        OffsetPageResponse<ReportResponse> last = await GetPageAsync("/api/v1/reports?pageSize=2&page=3");
        OffsetPageResponse<ReportResponse> pastEnd = await GetPageAsync("/api/v1/reports?pageSize=2&page=99");
        OffsetPageResponse<ReportResponse> hugePage = await GetPageAsync($"/api/v1/reports?pageSize=50&page={int.MaxValue}");

        middle.Items.Select(item => item.Id).Should().Equal(reports[2].Id, reports[3].Id);
        last.Items.Select(item => item.Id).Should().Equal(reports[4].Id);
        pastEnd.Items.Should().BeEmpty();
        pastEnd.Page.Should().Be(99);
        pastEnd.TotalCount.Should().Be(5);
        hugePage.Items.Should().BeEmpty();
        hugePage.TotalCount.Should().Be(5);
    }

    [Fact]
    public async Task ListingReports_AcceptsTheSmallestAndLargestPageSize()
    {
        await AddAdminAsync("identity-id", "username");
        User reporter = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "username-2");
        for (int index = 0; index < 3; index++)
        {
            User target = await NotificationTestData.AddUserAsync(_factory, $"identity-id-target-{index}", $"username-target-{index}");
            await AddReportForUserAsync(reporter, target, ReportReason.Spam);
            _factory.TimeProvider.Advance(TimeSpan.FromHours(1));
        }

        OffsetPageResponse<ReportResponse> smallest = await GetPageAsync("/api/v1/reports?pageSize=1");
        OffsetPageResponse<ReportResponse> largest = await GetPageAsync("/api/v1/reports?pageSize=50");

        smallest.Items.Should().ContainSingle();
        smallest.TotalCount.Should().Be(3);
        largest.Items.Should().HaveCount(3);
        largest.PageSize.Should().Be(50);
    }

    [Fact]
    public async Task ListingReports_OrdersReportsMadeAtTheSameInstantByIdAscendingOnEveryPage()
    {
        await AddAdminAsync("identity-id", "username");
        User reporter = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "username-2");
        List<Report> reports = [];
        for (int index = 0; index < 5; index++)
        {
            User target = await NotificationTestData.AddUserAsync(_factory, $"identity-id-target-{index}", $"username-target-{index}");
            reports.Add(await AddReportForUserAsync(reporter, target, ReportReason.Spam));
        }

        List<Guid> expected = reports.Select(report => report.Id).Order().ToList();

        OffsetPageResponse<ReportResponse> firstPage = await GetPageAsync("/api/v1/reports?pageSize=2&page=1");
        OffsetPageResponse<ReportResponse> secondPage = await GetPageAsync("/api/v1/reports?pageSize=2&page=2");
        OffsetPageResponse<ReportResponse> thirdPage = await GetPageAsync("/api/v1/reports?pageSize=2&page=3");
        OffsetPageResponse<ReportResponse> repeated = await GetPageAsync("/api/v1/reports?pageSize=2&page=1");

        firstPage.Items.Concat(secondPage.Items).Concat(thirdPage.Items).Select(item => item.Id)
            .Should().Equal(expected);
        repeated.Items.Select(item => item.Id).Should().Equal(firstPage.Items.Select(item => item.Id));
    }

    [Theory]
    [InlineData("?page=0", "page")]
    [InlineData("?page=-1", "page")]
    [InlineData("?pageSize=0", "pageSize")]
    [InlineData("?pageSize=51", "pageSize")]
    public async Task ListingReportsWithAnInvalidQuery_ReturnsValidationProblem(string query, string field)
    {
        await AddAdminAsync("identity-id", "username");
        using HttpRequestMessage request = NotificationTestData.CreateRequest(HttpMethod.Get, "/api/v1/reports" + query);

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        ValidationProblemDetails problem = (await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(TestContext.Current.CancellationToken))!;
        problem.Errors.Keys.Should().ContainSingle().Which.Should().Be(field);
    }

    [Fact]
    public async Task ListingReportsWithoutThePermission_ReturnsForbidden()
    {
        await NotificationTestData.AddUserAsync(_factory, "identity-id", "username");
        using HttpRequestMessage request = NotificationTestData.CreateRequest(HttpMethod.Get, "/api/v1/reports");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await AssertProblemAsync(response, HttpStatusCode.Forbidden, "Permission.Denied");
    }

    [Fact]
    public async Task Resolving_RecordsTheOutcomeAndRepeatingItChangesNothing()
    {
        User admin = await AddAdminAsync("identity-id", "username");
        User reporter = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "username-2");
        User target = await NotificationTestData.AddUserAsync(_factory, "identity-id-3", "username-3");
        Report report = await AddReportForUserAsync(reporter, target, ReportReason.Spam);
        DateTimeOffset resolvedAt = _factory.TimeProvider.GetUtcNow();
        using HttpRequestMessage first = PutRequest($"/api/v1/reports/{report.Id}/resolution", new { outcome = "Dismissed" });
        using HttpResponseMessage firstResponse = await _client.SendAsync(first, TestContext.Current.CancellationToken);
        _factory.TimeProvider.Advance(TimeSpan.FromHours(1));
        using HttpRequestMessage second = PutRequest($"/api/v1/reports/{report.Id}/resolution", new { outcome = "UserSuspended" });

        using HttpResponseMessage secondResponse = await _client.SendAsync(second, TestContext.Current.CancellationToken);

        firstResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        secondResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        Report stored = (await GetReportsAsync()).Should().ContainSingle().Subject;
        stored.Status.Should().Be(ReportStatus.Resolved);
        stored.Outcome.Should().Be(ReportOutcome.Dismissed);
        stored.ResolvedById.Should().Be(admin.Id);
        stored.ResolvedAt.Should().Be(resolvedAt);
        (await _factory.ExecuteDbContextAsync(dbContext => dbContext.Set<User>().AsNoTracking()
            .SingleAsync(user => user.Id == target.Id, TestContext.Current.CancellationToken))).IsSuspended.Should().BeFalse();
    }

    [Fact]
    public async Task ResolvingWithoutThePermission_ReturnsForbiddenAndKeepsTheReportOpen()
    {
        User caller = await NotificationTestData.AddUserAsync(_factory, "identity-id", "username");
        User target = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "username-2");
        Report report = await AddReportForUserAsync(caller, target, ReportReason.Spam);
        using HttpRequestMessage request = PutRequest($"/api/v1/reports/{report.Id}/resolution", new { outcome = "Dismissed" });

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await AssertProblemAsync(response, HttpStatusCode.Forbidden, "Permission.Denied");
        (await GetReportsAsync()).Should().ContainSingle().Which.Status.Should().Be(ReportStatus.Open);
    }

    [Fact]
    public async Task ResolvingAnUnknownReport_ReturnsNotFound()
    {
        await AddAdminAsync("identity-id", "username");
        using HttpRequestMessage request = PutRequest($"/api/v1/reports/{UnknownId}/resolution", new { outcome = "Dismissed" });

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "Report.NotFound");
    }

    [Fact]
    public async Task ResolvingWithAnUnknownOutcome_ReturnsValidationProblem()
    {
        await AddAdminAsync("identity-id", "username");
        using HttpRequestMessage request = PutRequest($"/api/v1/reports/{UnknownId}/resolution", new { outcome = "Unknown" });

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        ValidationProblemDetails problem = (await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(TestContext.Current.CancellationToken))!;
        problem.Errors.Keys.Should().ContainSingle().Which.Should().Be("outcome");
    }

    private static HttpRequestMessage PostRequest(string path, object body)
    {
        HttpRequestMessage request = NotificationTestData.CreateRequest(HttpMethod.Post, path);
        request.Content = JsonContent.Create(body);
        return request;
    }

    private static HttpRequestMessage PutRequest(string path, object body)
    {
        HttpRequestMessage request = NotificationTestData.CreateRequest(HttpMethod.Put, path);
        request.Content = JsonContent.Create(body);
        return request;
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        response.StatusCode.Should().Be(status);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        ProblemDetails problem = (await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken))!;
        problem.Status.Should().Be((int)status);
        problem.Extensions["code"].Should().BeOfType<JsonElement>().Which.GetString().Should().Be(code);
    }

    private async Task<OffsetPageResponse<ReportResponse>> GetPageAsync(string uri)
    {
        using HttpRequestMessage request = NotificationTestData.CreateRequest(HttpMethod.Get, uri);
        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        return (await response.Content.ReadFromJsonAsync<OffsetPageResponse<ReportResponse>>(TestContext.Current.CancellationToken))!;
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

    private Task<Report> AddReportForUserAsync(User reporter, User target, ReportReason reason)
    {
        Report report = Report.CreateForUser(reporter.Id, target.Id, reason, null, _factory.TimeProvider.GetUtcNow()).Value;
        return AddReportAsync(report);
    }

    private Task<Report> AddReportForPostAsync(User reporter, Post post, ReportReason reason)
    {
        Report report = Report.CreateForPost(reporter.Id, post.Id, post.AuthorId, reason, null, _factory.TimeProvider.GetUtcNow()).Value;
        return AddReportAsync(report);
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

    private async Task ResolveAsync(Guid reportId)
    {
        await _factory.ExecuteDbContextAsync(async db =>
        {
            Report report = await db.Set<Report>().SingleAsync(r => r.Id == reportId, TestContext.Current.CancellationToken);
            report.Resolve(ReportOutcome.Dismissed, report.ReporterId, _factory.TimeProvider.GetUtcNow());
            return await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
    }

    private Task<List<Report>> GetReportsAsync()
    {
        return _factory.ExecuteDbContextAsync(db => db.Set<Report>().AsNoTracking()
            .OrderBy(report => report.CreatedAt)
            .ThenBy(report => report.Id)
            .ToListAsync(TestContext.Current.CancellationToken));
    }
}
