using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Choco.Contract.AuditLogs;
using Choco.Domain.AuditLogs;
using Choco.Domain.Posts;
using Choco.Domain.Reports;
using Choco.Domain.Roles;
using Choco.Domain.Users;

namespace Choco.IntegrationTests.Audit;

public sealed class AccessAuditTests(ChocoApiFactory factory) : AuditTestsBase(factory)
{
    private const string Path = "/api/v1/admin/audit-logs";

    [Fact]
    public async Task CommandRefusedByTheHandlerWithPermissionDenied_RecordsAccessDeniedWithTheAttemptedRequest()
    {
        User author = await AddUserAsync("identity-id-2", "username-2");
        User reporter = await AddUserAsync("identity-id-3", "username-3");
        Role handler = Role.CreateSystem(new SystemRoleDefinition(
            "handler-key", "Handler", IsDefault: false, Permissions: [Permission.HandleReports]));
        await Factory.ExecuteDbContextAsync(dbContext =>
        {
            dbContext.Add(handler);
            return dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        User caller = await AddUserAsync("identity-id", "username", handler.Id);
        Post post = await AddPostAsync(author);
        Report report = Report.CreateForPost(reporter.Id, post.Id, author.Id, ReportReason.Spam, null, Factory.TimeProvider.GetUtcNow()).Value;
        await Factory.ExecuteDbContextAsync(dbContext =>
        {
            dbContext.Add(report);
            return dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        using HttpRequestMessage request = Request(
            HttpMethod.Put, $"/api/v1/reports/{report.Id}/resolution", new { outcome = "ContentRemoved" });

        using HttpResponseMessage response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        AuditLogEntry entry = (await ReadEntriesAsync()).Should().ContainSingle().Subject;
        ShouldBeEntry(entry, caller.Id, AuditAction.AccessDenied, AuditTargetType.Request, caller.Id, null, "Request: ResolveReportCommand");
    }

    [Fact]
    public async Task CommandRefusedWithAnotherForbiddenError_RecordsNoEntry()
    {
        User author = await AddUserAsync("identity-id-2", "username-2");
        await AddUserAsync("identity-id", "username");
        Post post = await AddPostAsync(author);
        using HttpRequestMessage request = Request(HttpMethod.Delete, $"/api/v1/posts/{post.Id}");

        using HttpResponseMessage response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ReadEntriesAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task QueryRefusedByTheAuthorizer_RecordsAccessDeniedWithTheAttemptedRequest()
    {
        User caller = await AddUserAsync("identity-id", "username");
        using HttpRequestMessage request = Request(HttpMethod.Get, Path);

        using HttpResponseMessage response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        AuditLogEntry entry = (await ReadEntriesAsync()).Should().ContainSingle().Subject;
        ShouldBeEntry(entry, caller.Id, AuditAction.AccessDenied, AuditTargetType.Request, caller.Id, null, "Request: GetAuditLogsQuery");
    }

    [Fact]
    public async Task RequestWithoutAToken_RecordsNoEntry()
    {
        using HttpRequestMessage request = new(HttpMethod.Get, Path);

        using HttpResponseMessage response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await ReadEntriesAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task RequestRefusedForAnotherReason_RecordsNoAccessDenied()
    {
        await AddUserAsync("identity-id", "username");
        using HttpRequestMessage request = Request(HttpMethod.Delete, $"/api/v1/posts/{Guid.NewGuid()}");

        using HttpResponseMessage response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ReadEntriesAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task ReadingTheAuditLog_RecordsAuditLogReadWithTheReaderAsTarget()
    {
        User administrator = await AddUserAsync("identity-id", "username", await GetAdministratorRoleIdAsync());
        using HttpRequestMessage request = Request(HttpMethod.Get, Path);

        using HttpResponseMessage response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        body.GetProperty("totalCount").GetInt32().Should().Be(0);
        AuditLogEntry entry = (await ReadEntriesAsync()).Should().ContainSingle().Subject;
        ShouldBeEntry(entry, administrator.Id, AuditAction.AuditLogRead, AuditTargetType.AuditLog, administrator.Id);
    }

    [Fact]
    public async Task ReadingTheAuditLogTwice_ShowsTheFirstReadInTheSecond()
    {
        await AddUserAsync("identity-id", "username", await GetAdministratorRoleIdAsync());
        using HttpRequestMessage first = Request(HttpMethod.Get, Path);
        using HttpResponseMessage firstResponse = await Client.SendAsync(first, TestContext.Current.CancellationToken);
        using HttpRequestMessage second = Request(HttpMethod.Get, $"{Path}?action=AuditLogRead&targetType=AuditLog");

        using HttpResponseMessage secondResponse = await Client.SendAsync(second, TestContext.Current.CancellationToken);

        secondResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement body = await secondResponse.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        body.GetProperty("totalCount").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task ReadingTheAuditLogManyTimesWithinTheWindow_RecordsOneEntry()
    {
        User administrator = await AddUserAsync("identity-id", "username", await GetAdministratorRoleIdAsync());

        for (int read = 0; read < 5; read++)
        {
            await ReadAuditLogAsync();
            Factory.TimeProvider.Advance(TimeSpan.FromSeconds(30));
        }

        AuditLogEntry entry = (await ReadEntriesAsync()).Should().ContainSingle().Subject;
        ShouldBeEntry(entry, administrator.Id, AuditAction.AuditLogRead, AuditTargetType.AuditLog, administrator.Id);
    }

    [Fact]
    public async Task ReadingTheAuditLogAtTheEndOfTheWindow_StillRecordsOneEntry()
    {
        await AddUserAsync("identity-id", "username", await GetAdministratorRoleIdAsync());
        await ReadAuditLogAsync();
        Factory.TimeProvider.Advance(TimeSpan.FromMinutes(GetAuditLogsQuery.RecordedReadWindowMinutes));

        await ReadAuditLogAsync();

        (await ReadEntriesAsync()).Should().ContainSingle();
    }

    [Fact]
    public async Task ReadingTheAuditLogAfterTheWindow_RecordsAnotherEntry()
    {
        User administrator = await AddUserAsync("identity-id", "username", await GetAdministratorRoleIdAsync());
        await ReadAuditLogAsync();
        Factory.TimeProvider.Advance(TimeSpan.FromMinutes(GetAuditLogsQuery.RecordedReadWindowMinutes) + TimeSpan.FromSeconds(1));

        await ReadAuditLogAsync();

        IReadOnlyList<AuditLogEntry> entries = await ReadEntriesAsync();
        entries.Should().HaveCount(2);
        entries.Should().OnlyContain(entry => entry.Action == AuditAction.AuditLogRead && entry.ActorId == administrator.Id);
    }

    [Fact]
    public async Task ReadingTheAuditLogWithinTheWindow_RecordsOneEntryForEachReader()
    {
        Guid administratorRoleId = await GetAdministratorRoleIdAsync();
        User first = await AddUserAsync("identity-id", "username", administratorRoleId);
        User second = await AddUserAsync("identity-id-2", "username-2", administratorRoleId);

        await ReadAuditLogAsync("identity-id");
        await ReadAuditLogAsync("identity-id-2");
        await ReadAuditLogAsync("identity-id");
        await ReadAuditLogAsync("identity-id-2");

        IReadOnlyList<AuditLogEntry> entries = await ReadEntriesAsync();
        entries.Select(entry => entry.ActorId).Should().BeEquivalentTo([first.Id, second.Id]);
    }

    [Fact]
    public async Task ReadingTheAuditLogWithinTheWindow_DoesNotHideOtherEntriesOfTheReader()
    {
        User administrator = await AddUserAsync("identity-id", "username", await GetAdministratorRoleIdAsync());
        await ReadAuditLogAsync();
        using HttpRequestMessage denied = Request(HttpMethod.Get, "/api/v1/admin/roles", identityId: "identity-id-2");
        await AddUserAsync("identity-id-2", "username-2");
        using HttpResponseMessage deniedResponse = await Client.SendAsync(denied, TestContext.Current.CancellationToken);

        await ReadAuditLogAsync();

        IReadOnlyList<AuditLogEntry> entries = await ReadEntriesAsync();
        entries.Count(entry => entry.Action == AuditAction.AuditLogRead && entry.ActorId == administrator.Id).Should().Be(1);
        entries.Should().Contain(entry => entry.Action == AuditAction.AccessDenied);
    }

    [Fact]
    public async Task ReadingTheAuditLogWithAnInvalidFilter_RecordsNoEntry()
    {
        await AddUserAsync("identity-id", "username", await GetAdministratorRoleIdAsync());
        using HttpRequestMessage request = Request(HttpMethod.Get, $"{Path}?page=0");

        using HttpResponseMessage response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadEntriesAsync()).Should().BeEmpty();
    }

    private async Task ReadAuditLogAsync(string identityId = "identity-id")
    {
        using HttpRequestMessage request = Request(HttpMethod.Get, Path, identityId: identityId);

        using HttpResponseMessage response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
