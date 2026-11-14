using System.Net;
using System.Net.Http.Json;

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

public sealed class ReportListSearchTests : IClassFixture<ChocoApiFactory>, IAsyncLifetime
{
    private const string ListPath = "/api/v1/admin/reports";

    private readonly ChocoApiFactory _factory;
    private readonly HttpClient _client;

    public ReportListSearchTests(ChocoApiFactory factory)
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
    public async Task List_SearchesByReportedUserPostAuthorPostWordsAndReporterUsername()
    {
        await AddAdminAsync("identity-id", "admin");
        User reporter = await AddUserAsync("identity-id-2", "reporter-needle", "Reporter Display");
        User otherReporter = await AddUserAsync("identity-id-3", "other-reporter", "Reporter Needle Display");
        User targetUser = await AddUserAsync("identity-id-4", "target-user", "Target Display");
        User author = await AddUserAsync("identity-id-5", "author-user", "Author Display");
        Post post = await NotificationTestData.AddPostAsync(_factory, author, "alpha beta");
        Report userReport = await AddReportForUserAsync(otherReporter, targetUser, ReportReason.Spam);
        _factory.TimeProvider.Advance(TimeSpan.FromHours(1));
        Report postReport = await AddReportForPostAsync(otherReporter, post, ReportReason.Spam);
        _factory.TimeProvider.Advance(TimeSpan.FromHours(1));
        Report byReporter = await AddReportForUserAsync(reporter, targetUser, ReportReason.Violence);

        (await ListAsync("q=target-user")).Should().Equal(byReporter.Id, userReport.Id);
        (await ListAsync("q=Target%20Display")).Should().Equal(byReporter.Id, userReport.Id);
        (await ListAsync("q=author-user")).Should().Equal(postReport.Id);
        (await ListAsync("q=beta%20alpha")).Should().Equal(postReport.Id);
        (await ListAsync("q=alpha%20missing")).Should().BeEmpty();
        (await ListAsync("q=reporter-needle")).Should().Equal(byReporter.Id);
        (await ListAsync("q=other-reporter")).Should().Equal(postReport.Id, userReport.Id);
    }

    [Fact]
    public async Task List_SearchDoesNotMatchTheReportersDisplayName()
    {
        await AddAdminAsync("identity-id", "admin");
        User reporter = await AddUserAsync("identity-id-2", "reporter", "Reporter Needle");
        User target = await AddUserAsync("identity-id-3", "target", "Target Display");
        await AddReportForUserAsync(reporter, target, ReportReason.Spam);

        (await ListAsync("q=needle")).Should().BeEmpty();
    }

    [Fact]
    public async Task List_ADeletedPostOrUserNoLongerMatchesByNameOrTextButItsReportsStillCome()
    {
        await AddAdminAsync("identity-id", "admin");
        User reporter = await AddUserAsync("identity-id-2", "reporter", "Reporter Display");
        User author = await AddUserAsync("identity-id-3", "author-needle", "Author Display");
        User deletedUser = await AddUserAsync("identity-id-4", "deleted-needle", "Deleted Display");
        User keptUser = await AddUserAsync("identity-id-5", "kept-needle", "Kept Display");
        Post deletedPost = await NotificationTestData.AddPostAsync(_factory, author, "needle words");
        Report keptReport = await AddReportForUserAsync(reporter, keptUser, ReportReason.Spam);
        _factory.TimeProvider.Advance(TimeSpan.FromHours(1));
        Report deletedUserReport = await AddReportForUserAsync(reporter, deletedUser, ReportReason.Violence);
        _factory.TimeProvider.Advance(TimeSpan.FromHours(1));
        Report deletedPostReport = await AddReportForPostAsync(reporter, deletedPost, ReportReason.Violence);
        await NotificationTestData.DeleteUserAsync(_factory, deletedUser);
        await NotificationTestData.DeletePostAsync(_factory, deletedPost);

        (await ListAsync(string.Empty)).Should().Equal(deletedPostReport.Id, deletedUserReport.Id, keptReport.Id);
        (await ListAsync("reason=Violence")).Should().Equal(deletedPostReport.Id, deletedUserReport.Id);
        (await ListAsync("q=deleted-needle")).Should().BeEmpty();
        (await ListAsync("q=needle%20words")).Should().BeEmpty();
        (await ListAsync("q=needle")).Should().Equal(keptReport.Id);
        (await ListAsync("q=reporter")).Should().Equal(deletedPostReport.Id, deletedUserReport.Id, keptReport.Id);
    }

    [Fact]
    public async Task List_FiltersByTheTimeTheReportWasMadeWithTheStartIncludedAndTheEndExcluded()
    {
        await AddAdminAsync("identity-id", "admin");
        User reporter = await AddUserAsync("identity-id-2", "reporter", "Reporter Display");
        User firstTarget = await AddUserAsync("identity-id-3", "target-first", "Target Display");
        User secondTarget = await AddUserAsync("identity-id-4", "target-second", "Target Display");
        User thirdTarget = await AddUserAsync("identity-id-5", "target-third", "Target Display");
        DateTimeOffset start = _factory.TimeProvider.GetUtcNow();
        Report first = await AddReportForUserAsync(reporter, firstTarget, ReportReason.Spam);
        _factory.TimeProvider.Advance(TimeSpan.FromHours(1));
        Report second = await AddReportForUserAsync(reporter, secondTarget, ReportReason.Harassment);
        _factory.TimeProvider.Advance(TimeSpan.FromHours(1));
        Report third = await AddReportForUserAsync(reporter, thirdTarget, ReportReason.Violence);

        (await ListAsync(Time("from", start.AddHours(1)))).Should().Equal(third.Id, second.Id);
        (await ListAsync(Time("to", start.AddHours(2)))).Should().Equal(second.Id, first.Id);
        (await ListAsync($"{Time("from", start.AddHours(1))}&{Time("to", start.AddHours(2))}")).Should().Equal(second.Id);
        (await ListAsync($"{Time("from", start.AddMinutes(1))}&{Time("to", start.AddMinutes(59))}")).Should().BeEmpty();
        (await ListAsync(Time("from", start.AddHours(3)))).Should().BeEmpty();
        (await ListAsync(Time("to", start))).Should().BeEmpty();
    }

    [Fact]
    public async Task List_SearchAndTimeCombineWithTheOtherFiltersAndTheTotalCountFollows()
    {
        User admin = await AddAdminAsync("identity-id", "admin");
        User firstReporter = await AddUserAsync("identity-id-2", "reporter-first", "Reporter Display");
        User secondReporter = await AddUserAsync("identity-id-3", "reporter-second", "Reporter Display");
        User thirdReporter = await AddUserAsync("identity-id-4", "reporter-third", "Reporter Display");
        User target = await AddUserAsync("identity-id-5", "needle-target", "Target Display");
        User other = await AddUserAsync("identity-id-6", "other", "Other Display");
        DateTimeOffset start = _factory.TimeProvider.GetUtcNow();
        Report early = await AddReportForUserAsync(firstReporter, target, ReportReason.Spam);
        _factory.TimeProvider.Advance(TimeSpan.FromHours(1));
        Report resolved = await AddReportForUserAsync(secondReporter, target, ReportReason.Spam);
        await ResolveAsync(resolved.Id, admin);
        _factory.TimeProvider.Advance(TimeSpan.FromHours(1));
        Report violence = await AddReportForUserAsync(thirdReporter, target, ReportReason.Violence);
        await AddReportForUserAsync(firstReporter, other, ReportReason.Spam);

        OffsetPageResponse<AdminReportResponse> search = await GetListAsync("q=needle");
        OffsetPageResponse<AdminReportResponse> searchAndTime = await GetListAsync($"q=needle&{Time("from", start.AddHours(1))}");
        OffsetPageResponse<AdminReportResponse> searchAndReason = await GetListAsync("q=needle&reason=Spam");
        OffsetPageResponse<AdminReportResponse> searchAndStatus = await GetListAsync("q=needle&status=Resolved");
        OffsetPageResponse<AdminReportResponse> paged = await GetListAsync("q=needle&pageSize=1&page=2");

        search.Items.Select(item => item.Id).Should().Equal(violence.Id, resolved.Id, early.Id);
        search.TotalCount.Should().Be(3);
        searchAndTime.Items.Select(item => item.Id).Should().Equal(violence.Id, resolved.Id);
        searchAndTime.TotalCount.Should().Be(2);
        searchAndReason.Items.Select(item => item.Id).Should().Equal(resolved.Id, early.Id);
        searchAndReason.TotalCount.Should().Be(2);
        searchAndStatus.Items.Select(item => item.Id).Should().Equal(resolved.Id);
        searchAndStatus.TotalCount.Should().Be(1);
        paged.Items.Select(item => item.Id).Should().Equal(resolved.Id);
        paged.TotalCount.Should().Be(3);
    }

    [Theory]
    [InlineData("")]
    [InlineData("%20%20%20")]
    public async Task List_ABlankSearchIsNoFilter(string value)
    {
        await AddAdminAsync("identity-id", "admin");
        User reporter = await AddUserAsync("identity-id-2", "reporter", "Reporter Display");
        User target = await AddUserAsync("identity-id-3", "target", "Target Display");
        Report report = await AddReportForUserAsync(reporter, target, ReportReason.Spam);

        (await ListAsync($"q={value}")).Should().Equal(report.Id);
    }

    [Fact]
    public async Task List_ASearchAtTheLimitIsAcceptedAndOneOverIsRefused()
    {
        await AddAdminAsync("identity-id", "admin");
        string atLimit = new('a', GetAdminReportsQuery.SearchMaxLength);
        string overLimit = new('a', GetAdminReportsQuery.SearchMaxLength + 1);

        (await ListAsync($"q={atLimit}")).Should().BeEmpty();

        using HttpRequestMessage request = NotificationTestData.CreateRequest(HttpMethod.Get, $"{ListPath}?q={overLimit}");
        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        await AssertValidationAsync(response, "q");
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(-1, 0)]
    public async Task List_ATimeRangeWhoseEndIsNotAfterItsStartIsRefused(int endHours, int startHours)
    {
        await AddAdminAsync("identity-id", "admin");
        DateTimeOffset start = _factory.TimeProvider.GetUtcNow().AddHours(startHours);
        DateTimeOffset end = start.AddHours(endHours);
        using HttpRequestMessage request = NotificationTestData.CreateRequest(
            HttpMethod.Get, $"{ListPath}?{Time("from", start)}&{Time("to", end)}");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await AssertValidationAsync(response, "to");
    }

    [Fact]
    public async Task List_ATimeRangeWithOnlyOneLimitIsAccepted()
    {
        await AddAdminAsync("identity-id", "admin");
        DateTimeOffset now = _factory.TimeProvider.GetUtcNow();

        (await ListAsync(Time("from", now))).Should().BeEmpty();
        (await ListAsync(Time("to", now))).Should().BeEmpty();
    }

    [Fact]
    public async Task SearchWithoutThePermission_ReturnsForbidden()
    {
        await AddUserAsync("identity-id", "username", "Display Name");
        using HttpRequestMessage request = NotificationTestData.CreateRequest(HttpMethod.Get, $"{ListPath}?q=needle");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static string Time(string name, DateTimeOffset value)
    {
        return $"{name}={Uri.EscapeDataString(value.ToString("o"))}";
    }

    private static async Task AssertValidationAsync(HttpResponseMessage response, string field)
    {
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        ValidationProblemDetails problem = (await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(TestContext.Current.CancellationToken))!;
        problem.Errors.Keys.Should().ContainSingle().Which.Should().Be(field);
    }

    private async Task<List<Guid>> ListAsync(string query)
    {
        OffsetPageResponse<AdminReportResponse> page = await GetListAsync(query);

        return page.Items.Select(item => item.Id).ToList();
    }

    private async Task<OffsetPageResponse<AdminReportResponse>> GetListAsync(string query)
    {
        using HttpRequestMessage request = NotificationTestData.CreateRequest(HttpMethod.Get, $"{ListPath}?{query}");
        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        return (await response.Content.ReadFromJsonAsync<OffsetPageResponse<AdminReportResponse>>(TestContext.Current.CancellationToken))!;
    }

    private async Task<User> AddUserAsync(string identityId, string username, string displayName)
    {
        User user = User.Create(identityId, username, displayName, null);
        await _factory.ExecuteDbContextAsync(db =>
        {
            db.Add(user);
            return db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        return user;
    }

    private async Task<User> AddAdminAsync(string identityId, string username)
    {
        Guid adminRoleId = await _factory.ExecuteDbContextAsync(db => db.Set<Role>()
            .Where(role => role.SystemKey == SystemRoles.Admin)
            .Select(role => role.Id)
            .SingleAsync(TestContext.Current.CancellationToken));
        User user = User.Create(identityId, username, "Admin Display", null);
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

    private async Task ResolveAsync(Guid reportId, User resolver)
    {
        await _factory.ExecuteDbContextAsync(async db =>
        {
            Report report = await db.Set<Report>().SingleAsync(r => r.Id == reportId, TestContext.Current.CancellationToken);
            report.Resolve(ReportOutcome.Dismissed, resolver.Id, _factory.TimeProvider.GetUtcNow());
            return await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
    }
}
