using System.Net;

using Choco.Domain.AuditLogs;
using Choco.Domain.FeedbackEntries;
using Choco.Domain.Posts;
using Choco.Domain.Reports;
using Choco.Domain.UserBlocks;
using Choco.Domain.Users;

namespace Choco.IntegrationTests.Audit;

public sealed class ModerationAuditTests(ChocoApiFactory factory) : AuditTestsBase(factory)
{
    private static readonly FeedbackContext Context = new(
        "/path", FeedbackLocale.Ja, FeedbackTheme.Dark, "user-agent", 1280, 720, "app-version");

    [Fact]
    public async Task SuspendUser_RecordsUserSuspendedWithTheReason()
    {
        User administrator = await AddUserAsync("identity-id", "username", await GetAdministratorRoleIdAsync());
        User target = await AddUserAsync("identity-id-2", "username-2");
        using HttpRequestMessage request = Request(
            HttpMethod.Put, $"/api/v1/admin/users/{target.Id}/suspension", new { reason = "  Repeated spam  " });

        using HttpResponseMessage response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        AuditLogEntry entry = (await ReadEntriesAsync()).Should().ContainSingle().Subject;
        ShouldBeEntry(entry, administrator.Id, AuditAction.UserSuspended, AuditTargetType.User, target.Id, target.Id, "Reason: Repeated spam");
    }

    [Fact]
    public async Task UnsuspendUser_RecordsUserUnsuspended()
    {
        User administrator = await AddUserAsync("identity-id", "username", await GetAdministratorRoleIdAsync());
        User target = await AddUserAsync("identity-id-2", "username-2");
        using HttpRequestMessage request = Request(HttpMethod.Delete, $"/api/v1/admin/users/{target.Id}/suspension");

        using HttpResponseMessage response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        AuditLogEntry entry = (await ReadEntriesAsync()).Should().ContainSingle().Subject;
        ShouldBeEntry(entry, administrator.Id, AuditAction.UserUnsuspended, AuditTargetType.User, target.Id, target.Id);
    }

    [Fact]
    public async Task ReportPost_RecordsPostReportedWithTheAuthorAndTheReason()
    {
        User author = await AddUserAsync("identity-id-2", "username-2");
        User reporter = await AddUserAsync("identity-id", "username");
        Post post = await AddPostAsync(author);
        using HttpRequestMessage request = Request(
            HttpMethod.Post, $"/api/v1/posts/{post.Id}/reports", new { reason = "Spam", details = "details" });

        using HttpResponseMessage response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        response.IsSuccessStatusCode.Should().BeTrue();
        AuditLogEntry entry = (await ReadEntriesAsync()).Should().ContainSingle().Subject;
        ShouldBeEntry(entry, reporter.Id, AuditAction.PostReported, AuditTargetType.Post, post.Id, author.Id, "Reason: Spam");
    }

    [Fact]
    public async Task ReportUser_RecordsUserReportedWithTheReason()
    {
        User target = await AddUserAsync("identity-id-2", "username-2");
        User reporter = await AddUserAsync("identity-id", "username");
        using HttpRequestMessage request = Request(
            HttpMethod.Post, $"/api/v1/users/{target.Id}/reports", new { reason = "Harassment", details = "details" });

        using HttpResponseMessage response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        response.IsSuccessStatusCode.Should().BeTrue();
        AuditLogEntry entry = (await ReadEntriesAsync()).Should().ContainSingle().Subject;
        ShouldBeEntry(entry, reporter.Id, AuditAction.UserReported, AuditTargetType.User, target.Id, target.Id, "Reason: Harassment");
    }

    [Fact]
    public async Task ResolveReportOfAPost_RecordsTheAuthorAsAffectedUser()
    {
        User author = await AddUserAsync("identity-id-2", "username-2");
        User reporter = await AddUserAsync("identity-id-3", "username-3");
        User administrator = await AddUserAsync("identity-id", "username", await GetAdministratorRoleIdAsync());
        Post post = await AddPostAsync(author);
        Report report = Report.CreateForPost(reporter.Id, post.Id, author.Id, ReportReason.Spam, null, Factory.TimeProvider.GetUtcNow()).Value;
        await AddAsync(report);
        using HttpRequestMessage request = Request(HttpMethod.Put, $"/api/v1/reports/{report.Id}/resolution", new { outcome = "Dismissed" });

        using HttpResponseMessage response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        response.IsSuccessStatusCode.Should().BeTrue();
        AuditLogEntry entry = (await ReadEntriesAsync()).Should().ContainSingle().Subject;
        ShouldBeEntry(
            entry, administrator.Id, AuditAction.ReportResolved, AuditTargetType.Report, report.Id, author.Id, "Outcome: Dismissed");
    }

    [Fact]
    public async Task ResolveReportOfAPostWithContentRemoved_RecordsTheOutcomeAndTheAuthorOfThePost()
    {
        User author = await AddUserAsync("identity-id-2", "username-2");
        User reporter = await AddUserAsync("identity-id-3", "username-3");
        User administrator = await AddUserAsync("identity-id", "username", await GetAdministratorRoleIdAsync());
        Post post = await AddPostAsync(author);
        Report report = Report.CreateForPost(reporter.Id, post.Id, author.Id, ReportReason.Spam, null, Factory.TimeProvider.GetUtcNow()).Value;
        await AddAsync(report);
        using HttpRequestMessage request = Request(HttpMethod.Put, $"/api/v1/reports/{report.Id}/resolution", new { outcome = "ContentRemoved" });

        using HttpResponseMessage response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        AuditLogEntry entry = (await ReadEntriesAsync()).Should().ContainSingle().Subject;
        ShouldBeEntry(
            entry, administrator.Id, AuditAction.ReportResolved, AuditTargetType.Report, report.Id, author.Id, "Outcome: ContentRemoved");
    }

    [Fact]
    public async Task ResolveReportOfAUser_RecordsTheReportedUserAsAffectedUser()
    {
        User target = await AddUserAsync("identity-id-2", "username-2");
        User reporter = await AddUserAsync("identity-id-3", "username-3");
        User administrator = await AddUserAsync("identity-id", "username", await GetAdministratorRoleIdAsync());
        Report report = Report.CreateForUser(reporter.Id, target.Id, ReportReason.Harassment, null, Factory.TimeProvider.GetUtcNow()).Value;
        await AddAsync(report);
        using HttpRequestMessage request = Request(HttpMethod.Put, $"/api/v1/reports/{report.Id}/resolution", new { outcome = "Dismissed" });

        using HttpResponseMessage response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        response.IsSuccessStatusCode.Should().BeTrue();
        AuditLogEntry entry = (await ReadEntriesAsync()).Should().ContainSingle().Subject;
        ShouldBeEntry(
            entry, administrator.Id, AuditAction.ReportResolved, AuditTargetType.Report, report.Id, target.Id, "Outcome: Dismissed");
    }

    [Fact]
    public async Task BlockUser_RecordsUserBlockedInsideTheSameTransactionAsTheBlock()
    {
        User target = await AddUserAsync("identity-id-2", "username-2");
        User blocker = await AddUserAsync("identity-id", "username");
        using HttpRequestMessage request = Request(HttpMethod.Put, $"/api/v1/users/{target.Id}/block");

        using HttpResponseMessage response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        response.IsSuccessStatusCode.Should().BeTrue();
        AuditLogEntry entry = (await ReadEntriesAsync()).Should().ContainSingle().Subject;
        ShouldBeEntry(entry, blocker.Id, AuditAction.UserBlocked, AuditTargetType.User, target.Id, target.Id);
    }

    [Fact]
    public async Task UnblockUser_RecordsUserUnblocked()
    {
        User target = await AddUserAsync("identity-id-2", "username-2");
        User blocker = await AddUserAsync("identity-id", "username");
        await AddAsync(UserBlock.Create(blocker.Id, target.Id, Factory.TimeProvider.GetUtcNow()).Value);
        using HttpRequestMessage request = Request(HttpMethod.Delete, $"/api/v1/users/{target.Id}/block");

        using HttpResponseMessage response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        response.IsSuccessStatusCode.Should().BeTrue();
        AuditLogEntry entry = (await ReadEntriesAsync()).Should().ContainSingle().Subject;
        ShouldBeEntry(entry, blocker.Id, AuditAction.UserUnblocked, AuditTargetType.User, target.Id, target.Id);
    }

    [Fact]
    public async Task UpdateFeedbackStatus_RecordsTheNewStatusAndTheAuthorAsAffectedUser()
    {
        User author = await AddUserAsync("identity-id-2", "username-2");
        User administrator = await AddUserAsync("identity-id", "username", await GetAdministratorRoleIdAsync());
        FeedbackEntry feedback = FeedbackEntry.Create(author.Id, FeedbackCategory.Bug, "message of the feedback", Context);
        await AddAsync(feedback);
        using HttpRequestMessage request = Request(HttpMethod.Patch, $"/api/v1/admin/feedback/{feedback.Id}", new { status = "in_progress" });

        using HttpResponseMessage response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        AuditLogEntry entry = (await ReadEntriesAsync()).Should().ContainSingle().Subject;
        ShouldBeEntry(
            entry, administrator.Id, AuditAction.FeedbackStatusChanged, AuditTargetType.Feedback, feedback.Id, author.Id, "Status: in_progress");
    }

    private Task AddAsync(object entity)
    {
        return Factory.ExecuteDbContextAsync(dbContext =>
        {
            dbContext.Add(entity);
            return dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
    }
}
