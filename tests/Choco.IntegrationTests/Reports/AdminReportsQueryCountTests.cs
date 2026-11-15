using System.Net;

using Choco.Domain.Posts;
using Choco.Domain.Reports;
using Choco.Domain.Roles;
using Choco.Domain.Users;
using Choco.IntegrationTests.Logging;
using Choco.IntegrationTests.Notifications;

using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Choco.IntegrationTests.Reports;

public sealed class AdminReportsQueryCountTests : IClassFixture<ChocoApiFactory>, IAsyncLifetime
{
    private const string Path = "/api/v1/admin/reports";
    private const string CommandCategory = "Microsoft.EntityFrameworkCore.Database.Command";

    private readonly ChocoApiFactory _factory;
    private readonly CapturingLoggerProvider _logs = new();
    private readonly HttpClient _client;

    public AdminReportsQueryCountTests(ChocoApiFactory factory)
    {
        _factory = factory;
        _client = factory.WithWebHostBuilder(builder => builder.ConfigureLogging(logging => logging
            .AddProvider(_logs)
            .AddFilter<CapturingLoggerProvider>(CommandCategory, LogLevel.Information)))
            .CreateClient();
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
    public async Task QueriesPerPage_DoNotGrowWithThePageSize()
    {
        User admin = await AddAdminAsync("identity-id", "admin");
        User reporter = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "reporter");
        for (int index = 0; index < 5; index++)
        {
            User author = await NotificationTestData.AddUserAsync(_factory, $"author-identity-id-{index}", $"author-{index}");
            Post post = await NotificationTestData.AddPostAsync(_factory, author, $"content-{index}");
            Report postReport = Report.CreateForPost(
                reporter.Id, post.Id, author.Id, ReportReason.Spam, null, _factory.TimeProvider.GetUtcNow()).Value;
            postReport.Resolve(ReportOutcome.Dismissed, admin.Id, _factory.TimeProvider.GetUtcNow());
            _factory.TimeProvider.Advance(TimeSpan.FromMinutes(1));
            User target = await NotificationTestData.AddUserAsync(_factory, $"target-identity-id-{index}", $"target-{index}");
            Report userReport = Report.CreateForUser(
                reporter.Id, target.Id, ReportReason.Spam, null, _factory.TimeProvider.GetUtcNow()).Value;
            _factory.TimeProvider.Advance(TimeSpan.FromMinutes(1));
            await _factory.ExecuteDbContextAsync(db =>
            {
                db.AddRange(postReport, userReport);
                return db.SaveChangesAsync(TestContext.Current.CancellationToken);
            });
        }
        await CountCommandsAsync($"{Path}?pageSize=2");

        int smallPage = await CountCommandsAsync($"{Path}?pageSize=2");
        int largePage = await CountCommandsAsync($"{Path}?pageSize=10");

        smallPage.Should().BePositive();
        largePage.Should().Be(smallPage);
    }

    private async Task<int> CountCommandsAsync(string uri)
    {
        int before = CommandCount();
        using HttpRequestMessage request = NotificationTestData.CreateRequest(HttpMethod.Get, uri);

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return CommandCount() - before;
    }

    private int CommandCount()
    {
        return _logs.Entries.Count(entry =>
            entry.Category == CommandCategory && entry.Message.StartsWith("Executed DbCommand", StringComparison.Ordinal));
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
}
