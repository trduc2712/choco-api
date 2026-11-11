using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Choco.Domain.FeedbackEntries;
using Choco.Domain.Reports;
using Choco.Domain.Roles;
using Choco.Domain.Users;
using Choco.IntegrationTests.Notifications;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Choco.IntegrationTests.AdminCounts;

public sealed class AdminCountsTests : IClassFixture<ChocoApiFactory>, IAsyncLifetime
{
    private const string Path = "/api/v1/admin/counts";
    private static readonly FeedbackContext Context = new("/path", FeedbackLocale.Ja, FeedbackTheme.Dark, "user-agent", 1280, 720, "app-version");
    private readonly ChocoApiFactory _factory;
    private readonly HttpClient _client;

    public AdminCountsTests(ChocoApiFactory factory)
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
    public async Task NoToken_ReturnsUnauthorized()
    {
        using HttpRequestMessage request = new(HttpMethod.Get, Path);

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Member_ReturnsForbidden()
    {
        await NotificationTestData.AddUserAsync(_factory, "identity-id", "username");
        using HttpRequestMessage request = NotificationTestData.CreateRequest(HttpMethod.Get, Path);

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        ProblemDetails problem = (await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken))!;
        problem.Extensions["code"].Should().BeOfType<JsonElement>().Which.GetString().Should().Be("Permission.Denied");
    }

    [Fact]
    public async Task Administrator_GetsTheNewFeedbackAndOpenReportCounts()
    {
        await AddUserWithAsync("identity-id", "username", SystemRoles.Admin);
        User author = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "username-2");
        User target = await NotificationTestData.AddUserAsync(_factory, "identity-id-3", "username-3");
        await AddFeedbackAsync(author, FeedbackStatus.New);
        await AddFeedbackAsync(author, FeedbackStatus.New);
        await AddFeedbackAsync(author, FeedbackStatus.InProgress);
        await AddFeedbackAsync(author, FeedbackStatus.Resolved);
        Report resolved = await AddReportAsync(author, target, ReportReason.Spam);
        await ResolveAsync(resolved.Id);
        await AddReportAsync(author, target, ReportReason.Harassment);
        await AddReportAsync(target, author, ReportReason.Spam);
        using HttpRequestMessage request = NotificationTestData.CreateRequest(HttpMethod.Get, Path);

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        body.EnumerateObject().Select(property => property.Name).Should().BeEquivalentTo("newFeedback", "openReports");
        body.GetProperty("newFeedback").GetInt32().Should().Be(2);
        body.GetProperty("openReports").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task Administrator_WithNothingWaiting_GetsZeroCounts()
    {
        await AddUserWithAsync("identity-id", "username", SystemRoles.Admin);
        using HttpRequestMessage request = NotificationTestData.CreateRequest(HttpMethod.Get, Path);

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        body.GetProperty("newFeedback").GetInt32().Should().Be(0);
        body.GetProperty("openReports").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task UserWhoOnlyHandlesReports_GetsNullForFeedback()
    {
        await AddUserWithPermissionAsync("identity-id", "username", Permission.HandleReports);
        User author = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "username-2");
        User target = await NotificationTestData.AddUserAsync(_factory, "identity-id-3", "username-3");
        await AddFeedbackAsync(author, FeedbackStatus.New);
        await AddReportAsync(author, target, ReportReason.Spam);
        using HttpRequestMessage request = NotificationTestData.CreateRequest(HttpMethod.Get, Path);

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        body.GetProperty("newFeedback").ValueKind.Should().Be(JsonValueKind.Null);
        body.GetProperty("openReports").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task UserWhoOnlyReadsFeedback_GetsNullForReports()
    {
        await AddUserWithPermissionAsync("identity-id", "username", Permission.ReadAnyFeedback);
        User author = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "username-2");
        User target = await NotificationTestData.AddUserAsync(_factory, "identity-id-3", "username-3");
        await AddFeedbackAsync(author, FeedbackStatus.New);
        await AddReportAsync(author, target, ReportReason.Spam);
        using HttpRequestMessage request = NotificationTestData.CreateRequest(HttpMethod.Get, Path);

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        body.GetProperty("newFeedback").GetInt32().Should().Be(1);
        body.GetProperty("openReports").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task UserWithAnotherPermissionOnly_ReturnsForbidden()
    {
        await AddUserWithPermissionAsync("identity-id", "username", Permission.ReadAuditLog);
        using HttpRequestMessage request = NotificationTestData.CreateRequest(HttpMethod.Get, Path);

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private async Task<User> AddUserWithAsync(string identityId, string username, string systemKey)
    {
        Guid roleId = await _factory.ExecuteDbContextAsync(db => db.Set<Role>()
            .Where(role => role.SystemKey == systemKey)
            .Select(role => role.Id)
            .SingleAsync(TestContext.Current.CancellationToken));

        return await AddUserWithRoleAsync(identityId, username, roleId);
    }

    private async Task<User> AddUserWithPermissionAsync(string identityId, string username, string permission)
    {
        Role role = Role.CreateSystem(new SystemRoleDefinition("role-key", "Role", IsDefault: false, Permissions: [permission]));
        await _factory.ExecuteDbContextAsync(db =>
        {
            db.Add(role);
            return db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        return await AddUserWithRoleAsync(identityId, username, role.Id);
    }

    private async Task<User> AddUserWithRoleAsync(string identityId, string username, Guid roleId)
    {
        User user = User.Create(identityId, username, "Display Name", null);
        user.AssignRole(roleId, null, ChocoApiFactory.StartTime);
        await _factory.ExecuteDbContextAsync(db =>
        {
            db.Add(user);
            return db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        return user;
    }

    private async Task AddFeedbackAsync(User author, FeedbackStatus status)
    {
        FeedbackEntry entry = FeedbackEntry.Create(author.Id, FeedbackCategory.Bug, "feedback message", Context);
        entry.ChangeStatus(status);
        await _factory.ExecuteDbContextAsync(db =>
        {
            db.Add(entry);
            return db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
    }

    private async Task<Report> AddReportAsync(User reporter, User target, ReportReason reason)
    {
        Report report = Report.CreateForUser(reporter.Id, target.Id, reason, null, _factory.TimeProvider.GetUtcNow()).Value;
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
}
