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

public sealed class ReportQueueSearchTests : IClassFixture<ChocoApiFactory>, IAsyncLifetime
{
    private const string QueuePath = "/api/v1/reports";

    private readonly ChocoApiFactory _factory;
    private readonly HttpClient _client;

    public ReportQueueSearchTests(ChocoApiFactory factory)
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
    public async Task Queue_SearchesReportedUsersByUsernameAndDisplayNameAsOnePhrase()
    {
        await AddAdminAsync("identity-id", "admin");
        User reporter = await AddUserAsync("identity-id-2", "reporter", "Reporter Display");
        User first = await AddUserAsync("identity-id-3", "username-first", "Needle Name");
        User second = await AddUserAsync("identity-id-4", "username-second", "Other Name");
        Report firstReport = await AddReportForUserAsync(reporter, first, ReportReason.Spam);
        _factory.TimeProvider.Advance(TimeSpan.FromHours(1));
        Report secondReport = await AddReportForUserAsync(reporter, second, ReportReason.Spam);

        (await QueueAsync("q=first")).Should().Equal(firstReport.Id);
        (await QueueAsync("q=NEEDLE")).Should().Equal(firstReport.Id);
        (await QueueAsync("q=Needle%20Name")).Should().Equal(firstReport.Id);
        (await QueueAsync("q=Name%20Needle")).Should().BeEmpty();
        (await QueueAsync("q=name")).Should().Equal(firstReport.Id, secondReport.Id);
        (await QueueAsync("q=reporter")).Should().BeEmpty();
    }

    [Fact]
    public async Task Queue_SearchesReportedPostsByAuthorNameAndByAllTheWordsOfThePost()
    {
        await AddAdminAsync("identity-id", "admin");
        User reporter = await AddUserAsync("identity-id-2", "reporter", "Reporter Display");
        User firstAuthor = await AddUserAsync("identity-id-3", "author-first", "Author Display");
        User secondAuthor = await AddUserAsync("identity-id-4", "author-second", "Writer Display");
        Post firstPost = await NotificationTestData.AddPostAsync(_factory, firstAuthor, "alpha beta");
        Post secondPost = await NotificationTestData.AddPostAsync(_factory, secondAuthor, "gamma delta");
        Report firstReport = await AddReportForPostAsync(reporter, firstPost, ReportReason.Spam);
        _factory.TimeProvider.Advance(TimeSpan.FromHours(1));
        Report secondReport = await AddReportForPostAsync(reporter, secondPost, ReportReason.Spam);

        (await QueueAsync("q=author-first")).Should().Equal(firstReport.Id);
        (await QueueAsync("q=Author%20Display")).Should().Equal(firstReport.Id);
        (await QueueAsync("q=Display%20Author")).Should().BeEmpty();
        (await QueueAsync("q=beta%20alpha")).Should().Equal(firstReport.Id);
        (await QueueAsync("q=alpha%20missing")).Should().BeEmpty();
        (await QueueAsync("q=GAMMA")).Should().Equal(secondReport.Id);
        (await QueueAsync("q=display")).Should().Equal(firstReport.Id, secondReport.Id);
    }

    [Fact]
    public async Task Queue_SearchMatchesAUserAndAPostInTheSameQuery_IgnoringCaseAndAccents()
    {
        await AddAdminAsync("identity-id", "admin");
        User reporter = await AddUserAsync("identity-id-2", "reporter", "Reporter Display");
        User author = await AddUserAsync("identity-id-3", "author", "Author Display");
        User target = await AddUserAsync("identity-id-4", "target", "Café Owner");
        Post post = await NotificationTestData.AddPostAsync(_factory, author, "Café menu");
        Report userReport = await AddReportForUserAsync(reporter, target, ReportReason.Spam);
        _factory.TimeProvider.Advance(TimeSpan.FromHours(1));
        Report postReport = await AddReportForPostAsync(reporter, post, ReportReason.Spam);

        (await QueueAsync("q=cafe")).Should().Equal(userReport.Id, postReport.Id);
        (await QueueAsync("q=CAF%C3%89")).Should().Equal(userReport.Id, postReport.Id);
    }

    [Fact]
    public async Task Queue_SearchCombinesWithTheOtherFiltersAndTheTotalCountFollowsIt()
    {
        await AddAdminAsync("identity-id", "admin");
        User reporter = await AddUserAsync("identity-id-2", "reporter", "Reporter Display");
        User author = await AddUserAsync("identity-id-3", "needle-author", "Author Display");
        User target = await AddUserAsync("identity-id-4", "needle-target", "Target Display");
        User other = await AddUserAsync("identity-id-5", "other", "Other Display");
        Post post = await NotificationTestData.AddPostAsync(_factory, author, "content");
        Report postReport = await AddReportForPostAsync(reporter, post, ReportReason.Spam);
        _factory.TimeProvider.Advance(TimeSpan.FromHours(1));
        Report userReport = await AddReportForUserAsync(reporter, target, ReportReason.Violence);
        _factory.TimeProvider.Advance(TimeSpan.FromHours(1));
        await AddReportForUserAsync(reporter, other, ReportReason.Spam);

        OffsetPageResponse<ReportQueueItemResponse> all = await GetQueueAsync("q=needle");
        OffsetPageResponse<ReportQueueItemResponse> users = await GetQueueAsync("q=needle&targetType=User");
        OffsetPageResponse<ReportQueueItemResponse> spam = await GetQueueAsync("q=needle&reason=Spam");
        OffsetPageResponse<ReportQueueItemResponse> paged = await GetQueueAsync("q=needle&pageSize=1&page=2");

        all.Items.Select(item => item.ReportId).Should().Equal(postReport.Id, userReport.Id);
        all.TotalCount.Should().Be(2);
        users.Items.Select(item => item.ReportId).Should().Equal(userReport.Id);
        users.TotalCount.Should().Be(1);
        spam.Items.Select(item => item.ReportId).Should().Equal(postReport.Id);
        spam.TotalCount.Should().Be(1);
        paged.Items.Select(item => item.ReportId).Should().Equal(userReport.Id);
        paged.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task Queue_SearchKeepsAllTheOpenReportsOfAMatchingRow()
    {
        await AddAdminAsync("identity-id", "admin");
        User firstReporter = await AddUserAsync("identity-id-2", "reporter-first", "Reporter Display");
        User secondReporter = await AddUserAsync("identity-id-3", "reporter-second", "Reporter Display");
        User target = await AddUserAsync("identity-id-4", "target", "Needle Name");
        await AddReportForUserAsync(firstReporter, target, ReportReason.Spam);
        await AddReportForUserAsync(secondReporter, target, ReportReason.Harassment);

        OffsetPageResponse<ReportQueueItemResponse> page = await GetQueueAsync("q=needle");

        ReportQueueItemResponse item = page.Items.Should().ContainSingle().Subject;
        item.OpenReportCount.Should().Be(2);
        item.Reports.Should().HaveCount(2);
    }

    [Theory]
    [InlineData("")]
    [InlineData("%20%20%20")]
    public async Task Queue_ABlankSearchIsNoFilter(string value)
    {
        await AddAdminAsync("identity-id", "admin");
        User reporter = await AddUserAsync("identity-id-2", "reporter", "Reporter Display");
        User first = await AddUserAsync("identity-id-3", "username-first", "First Display");
        User second = await AddUserAsync("identity-id-4", "username-second", "Second Display");
        Report firstReport = await AddReportForUserAsync(reporter, first, ReportReason.Spam);
        _factory.TimeProvider.Advance(TimeSpan.FromHours(1));
        Report secondReport = await AddReportForUserAsync(reporter, second, ReportReason.Spam);

        (await QueueAsync($"q={value}")).Should().Equal(firstReport.Id, secondReport.Id);
    }

    [Fact]
    public async Task Queue_SearchTreatsWildcardsAsPlainCharacters()
    {
        await AddAdminAsync("identity-id", "admin");
        User reporter = await AddUserAsync("identity-id-2", "reporter", "Reporter Display");
        User percent = await AddUserAsync("identity-id-3", "username-percent", "Fifty % Off");
        User plain = await AddUserAsync("identity-id-4", "username-plain", "Plain Display");
        Report percentReport = await AddReportForUserAsync(reporter, percent, ReportReason.Spam);
        _factory.TimeProvider.Advance(TimeSpan.FromHours(1));
        await AddReportForUserAsync(reporter, plain, ReportReason.Spam);

        (await QueueAsync("q=%25")).Should().Equal(percentReport.Id);
        (await QueueAsync("q=_")).Should().BeEmpty();
    }

    [Fact]
    public async Task Queue_ADeletedPostOrUserNoLongerMatches()
    {
        await AddAdminAsync("identity-id", "admin");
        User reporter = await AddUserAsync("identity-id-2", "reporter", "Reporter Display");
        User author = await AddUserAsync("identity-id-3", "author", "Author Display");
        User deletedUser = await AddUserAsync("identity-id-4", "deleted-needle", "Deleted Display");
        User keptUser = await AddUserAsync("identity-id-5", "kept-needle", "Kept Display");
        Post deletedPost = await NotificationTestData.AddPostAsync(_factory, author, "needle words");
        Report keptReport = await AddReportForUserAsync(reporter, keptUser, ReportReason.Spam);
        await AddReportForUserAsync(reporter, deletedUser, ReportReason.Spam);
        await AddReportForPostAsync(reporter, deletedPost, ReportReason.Spam);
        await NotificationTestData.DeleteUserAsync(_factory, deletedUser);
        await NotificationTestData.DeletePostAsync(_factory, deletedPost);

        (await QueueAsync("q=needle")).Should().Equal(keptReport.Id);
    }

    [Fact]
    public async Task Queue_OnlyTheFirstFiveWordsOfThePostCount()
    {
        await AddAdminAsync("identity-id", "admin");
        User reporter = await AddUserAsync("identity-id-2", "reporter", "Reporter Display");
        User author = await AddUserAsync("identity-id-3", "author", "Author Display");
        Post post = await NotificationTestData.AddPostAsync(_factory, author, "one two three four five");
        Report report = await AddReportForPostAsync(reporter, post, ReportReason.Spam);

        (await QueueAsync("q=one%20two%20three%20four%20five")).Should().Equal(report.Id);
        (await QueueAsync("q=one%20two%20three%20four%20five%20six")).Should().Equal(report.Id);
    }

    [Fact]
    public async Task Queue_ASearchAtTheLimitIsAcceptedAndOneOverIsRefused()
    {
        await AddAdminAsync("identity-id", "admin");
        string atLimit = new('a', GetReportsQuery.SearchMaxLength);
        string overLimit = new('a', GetReportsQuery.SearchMaxLength + 1);

        (await QueueAsync($"q={atLimit}")).Should().BeEmpty();

        using HttpRequestMessage request = NotificationTestData.CreateRequest(HttpMethod.Get, $"{QueuePath}?q={overLimit}");
        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        await AssertValidationAsync(response, "q");
    }

    [Fact]
    public async Task SearchWithoutThePermission_ReturnsForbidden()
    {
        await AddUserAsync("identity-id", "username", "Display Name");
        using HttpRequestMessage request = NotificationTestData.CreateRequest(HttpMethod.Get, $"{QueuePath}?q=needle");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static async Task AssertValidationAsync(HttpResponseMessage response, string field)
    {
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        ValidationProblemDetails problem = (await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(TestContext.Current.CancellationToken))!;
        problem.Errors.Keys.Should().ContainSingle().Which.Should().Be(field);
    }

    private async Task<List<Guid>> QueueAsync(string query)
    {
        OffsetPageResponse<ReportQueueItemResponse> page = await GetQueueAsync(query);

        return page.Items.Select(item => item.ReportId).ToList();
    }

    private async Task<OffsetPageResponse<ReportQueueItemResponse>> GetQueueAsync(string query)
    {
        using HttpRequestMessage request = NotificationTestData.CreateRequest(HttpMethod.Get, $"{QueuePath}?{query}");
        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        return (await response.Content.ReadFromJsonAsync<OffsetPageResponse<ReportQueueItemResponse>>(TestContext.Current.CancellationToken))!;
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
}
