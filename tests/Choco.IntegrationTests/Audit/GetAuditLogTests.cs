using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Choco.Contract.AuditLogs;
using Choco.Domain.AuditLogs;
using Choco.Domain.Comments;
using Choco.Domain.FeedbackEntries;
using Choco.Domain.Posts;
using Choco.Domain.Reports;
using Choco.Domain.Roles;
using Choco.Domain.Users;

using Microsoft.AspNetCore.Mvc;

namespace Choco.IntegrationTests.Audit;

public sealed class GetAuditLogTests(ChocoApiFactory factory) : AuditTestsBase(factory)
{
    private const string Path = "/api/v1/admin/audit-logs";

    private static readonly DateTimeOffset Time = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly Guid TargetId = new("7a3d9c2b-8e1f-4b6a-a5d4-1c0e9f8b7a21");
    private static readonly Guid OtherTargetId = new("2b8e4d6f-1a3c-4e9b-8f7d-6c5a4b3e2d10");
    private static readonly FeedbackContext Context = new("/path", FeedbackLocale.Ja, FeedbackTheme.Dark, "user-agent", 1280, 720, "app-version");

    [Fact]
    public async Task NoToken_ReturnsUnauthorized()
    {
        using HttpRequestMessage request = new(HttpMethod.Get, $"{Path}/{TargetId}");

        using HttpResponseMessage response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Member_ReturnsForbidden()
    {
        User member = await AddUserAsync("identity-id", "username");
        Guid entryId = await AddEntryAsync(member.Id, Time, AuditAction.PostEdited, AuditTargetType.Post, TargetId);
        using HttpRequestMessage request = Request(HttpMethod.Get, $"{Path}/{entryId}");

        using HttpResponseMessage response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UnknownId_ReturnsNotFound()
    {
        await AddAdministratorAsync();
        using HttpRequestMessage request = Request(HttpMethod.Get, $"{Path}/{TargetId}");

        using HttpResponseMessage response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        ProblemDetails problem = (await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken))!;
        problem.Extensions["code"].Should().BeOfType<JsonElement>().Which.GetString().Should().Be("AuditLog.NotFound");
    }

    [Fact]
    public async Task EmptyId_ReturnsValidationProblem()
    {
        await AddAdministratorAsync();
        using HttpRequestMessage request = Request(HttpMethod.Get, $"{Path}/{Guid.Empty}");

        using HttpResponseMessage response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        ValidationProblemDetails problem = (await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(TestContext.Current.CancellationToken))!;
        problem.Errors.Keys.Should().BeEquivalentTo("id");
    }

    [Fact]
    public async Task FeedbackEntry_HasTheFeedbackSummaryWithAnExcerpt()
    {
        User administrator = await AddAdministratorAsync();
        User author = await AddUserAsync("identity-id-2", "username-2");
        FeedbackEntry feedback = FeedbackEntry.Create(author.Id, FeedbackCategory.Bug, new string('m', 200), Context);
        feedback.ChangeStatus(FeedbackStatus.InProgress);
        await AddAsync(feedback);
        Guid entryId = await AddEntryAsync(
            administrator.Id, Time, AuditAction.FeedbackStatusChanged, AuditTargetType.Feedback, feedback.Id, author.Id,
            changes: [new AuditChange("status", "new", "in_progress")]);

        JsonElement body = await GetJsonAsync($"{Path}/{entryId}");

        JsonElement summary = body.GetProperty("targetSummary");
        summary.GetProperty("excerpt").GetString().Should().Be(new string('m', 160) + "…");
        summary.GetProperty("author").GetProperty("username").GetString().Should().Be("username-2");
        summary.GetProperty("category").GetString().Should().Be("bug");
        summary.GetProperty("status").GetString().Should().Be("in_progress");
        summary.EnumerateObject().Select(property => property.Name)
            .Should().BeEquivalentTo("excerpt", "author", "category", "status");
    }

    [Fact]
    public async Task DeletedPostEntry_HasASummaryMarkedDeleted()
    {
        User administrator = await AddAdministratorAsync();
        User author = await AddUserAsync("identity-id-2", "username-2");
        Post post = await AddPostAsync(author);
        await Factory.ExecuteDbContextAsync(dbContext =>
        {
            dbContext.Remove(dbContext.Set<Post>().Single(other => other.Id == post.Id));
            return dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        Guid entryId = await AddEntryAsync(administrator.Id, Time, AuditAction.PostDeleted, AuditTargetType.Post, post.Id, author.Id);

        JsonElement body = await GetJsonAsync($"{Path}/{entryId}");

        JsonElement summary = body.GetProperty("targetSummary");
        summary.GetProperty("excerpt").GetString().Should().Be("content");
        summary.GetProperty("author").GetProperty("id").GetGuid().Should().Be(author.Id);
        summary.GetProperty("isDeleted").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task CommentEntry_HasTheCommentSummaryWithItsPost()
    {
        User administrator = await AddAdministratorAsync();
        User author = await AddUserAsync("identity-id-2", "username-2");
        Post post = await AddPostAsync(author);
        Comment comment = Comment.Create(post.Id, author.Id, "comment-content");
        await AddAsync(comment);
        Guid entryId = await AddEntryAsync(author.Id, Time, AuditAction.CommentCreated, AuditTargetType.Comment, comment.Id);

        JsonElement body = await GetJsonAsync($"{Path}/{entryId}");

        JsonElement summary = body.GetProperty("targetSummary");
        summary.GetProperty("excerpt").GetString().Should().Be("comment-content");
        summary.GetProperty("author").GetProperty("username").GetString().Should().Be("username-2");
        summary.GetProperty("isDeleted").GetBoolean().Should().BeFalse();
        summary.GetProperty("postId").GetGuid().Should().Be(post.Id);
    }

    [Fact]
    public async Task ReportEntry_HasTheReportSummaryWithTheReportedObject()
    {
        User administrator = await AddAdministratorAsync();
        User reporter = await AddUserAsync("identity-id-2", "username-2");
        User reported = await AddUserAsync("identity-id-3", "username-3");
        Report report = Report.CreateForUser(reporter.Id, reported.Id, ReportReason.Spam, "report-details", Time).Value;
        report.Resolve(ReportOutcome.Dismissed, administrator.Id, Time);
        await AddAsync(report);
        Guid entryId = await AddEntryAsync(administrator.Id, Time, AuditAction.ReportResolved, AuditTargetType.Report, report.Id, reporter.Id);

        JsonElement body = await GetJsonAsync($"{Path}/{entryId}");

        JsonElement summary = body.GetProperty("targetSummary");
        summary.GetProperty("excerpt").GetString().Should().Be("report-details");
        summary.GetProperty("status").GetString().Should().Be("Resolved");
        summary.GetProperty("reason").GetString().Should().Be("Spam");
        summary.GetProperty("outcome").GetString().Should().Be("Dismissed");
        summary.GetProperty("reportedType").GetString().Should().Be("User");
        summary.GetProperty("reportedId").GetGuid().Should().Be(reported.Id);
        summary.GetProperty("reporter").GetProperty("username").GetString().Should().Be("username-2");
    }

    [Fact]
    public async Task UserEntry_HasTheUserSummary()
    {
        User administrator = await AddAdministratorAsync();
        User target = User.Create("identity-id-2", "username-2", "Display Name 2", "https://example.com/avatar.png");
        target.Suspend("suspension-reason", administrator.Id, Time);
        await AddAsync(target);
        Guid entryId = await AddEntryAsync(administrator.Id, Time, AuditAction.UserSuspended, AuditTargetType.User, target.Id, target.Id);

        JsonElement body = await GetJsonAsync($"{Path}/{entryId}");

        JsonElement summary = body.GetProperty("targetSummary");
        summary.GetProperty("username").GetString().Should().Be("username-2");
        summary.GetProperty("displayName").GetString().Should().Be("Display Name 2");
        summary.GetProperty("avatarUrl").GetString().Should().Be("https://example.com/avatar.png");
        summary.GetProperty("isSuspended").GetBoolean().Should().BeTrue();
        summary.GetProperty("isDeleted").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task RoleEntry_HasTheRoleSummaryWithItsHolders()
    {
        User administrator = await AddAdministratorAsync();
        Role role = Role.Create("Reviewer", [Permission.HandleReports, Permission.ReadAnyFeedback]);
        await AddAsync(role);
        await AddUserAsync("identity-id-2", "username-2", role.Id);
        Guid entryId = await AddEntryAsync(administrator.Id, Time, AuditAction.RoleCreated, AuditTargetType.Role, role.Id);

        JsonElement body = await GetJsonAsync($"{Path}/{entryId}");

        JsonElement summary = body.GetProperty("targetSummary");
        summary.GetProperty("name").GetString().Should().Be("Reviewer");
        summary.GetProperty("permissionCount").GetInt32().Should().Be(2);
        summary.GetProperty("holderCount").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task EntryOfADeletedRole_HasNoSummary()
    {
        User administrator = await AddAdministratorAsync();
        Guid entryId = await AddEntryAsync(administrator.Id, Time, AuditAction.RoleDeleted, AuditTargetType.Role, TargetId);

        JsonElement body = await GetJsonAsync($"{Path}/{entryId}");

        body.TryGetProperty("targetSummary", out _).Should().BeFalse();
        body.GetProperty("targetHistory").GetProperty("totalCount").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task ReaderWithoutTheContentPermission_GetsTheSummaryWithoutTheExcerpt()
    {
        Role auditor = Role.Create("Auditor", [Permission.ReadAuditLog]);
        await AddAsync(auditor);
        User reader = await AddUserAsync("identity-id", "username", auditor.Id);
        User author = await AddUserAsync("identity-id-2", "username-2");
        Post post = await AddPostAsync(author);
        Guid entryId = await AddEntryAsync(reader.Id, Time, AuditAction.PostEdited, AuditTargetType.Post, post.Id, author.Id);

        JsonElement body = await GetJsonAsync($"{Path}/{entryId}");

        JsonElement summary = body.GetProperty("targetSummary");
        summary.TryGetProperty("excerpt", out _).Should().BeFalse();
        summary.GetProperty("author").GetProperty("id").GetGuid().Should().Be(author.Id);
        summary.GetProperty("isDeleted").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task EntryInALongHistory_ListsFiveEntriesAroundItWithTheirValuesAndTheTotal()
    {
        User administrator = await AddAdministratorAsync();
        string[] statuses = ["in_progress", "wont_fix", "in_progress", "resolved", "wont_fix", "resolved"];
        List<Guid> ids = [await AddEntryAsync(administrator.Id, Time, AuditAction.FeedbackSent, AuditTargetType.Feedback, TargetId)];
        for (int index = 0; index < statuses.Length; index++)
        {
            ids.Add(await AddEntryAsync(
                administrator.Id, Time.AddDays(index + 1), AuditAction.FeedbackStatusChanged, AuditTargetType.Feedback, TargetId,
                changes: [new AuditChange("status", index == 0 ? "new" : statuses[index - 1], statuses[index])]));
        }

        JsonElement body = await GetJsonAsync($"{Path}/{ids[3]}");

        JsonElement history = body.GetProperty("targetHistory");
        history.GetProperty("totalCount").GetInt32().Should().Be(7);
        JsonElement[] recent = history.GetProperty("recent").EnumerateArray().ToArray();
        recent.Select(item => item.GetProperty("id").GetGuid()).Should().Equal(ids[1], ids[2], ids[3], ids[4], ids[5]);
        recent.Select(item => item.GetProperty("value").GetString()).Should().Equal("in_progress", "wont_fix", "in_progress", "resolved", "wont_fix");
        recent[1].GetProperty("action").GetString().Should().Be("FeedbackStatusChanged");
        recent[1].GetProperty("occurredAt").GetDateTimeOffset().Should().Be(Time.AddDays(2));
    }

    [Fact]
    public async Task NewestEntry_FillsTheHistoryWithOlderEntries()
    {
        User administrator = await AddAdministratorAsync();
        List<Guid> ids = [];
        for (int index = 0; index < 6; index++)
        {
            ids.Add(await AddEntryAsync(administrator.Id, Time.AddDays(index), AuditAction.PostEdited, AuditTargetType.Post, TargetId));
        }

        JsonElement body = await GetJsonAsync($"{Path}/{ids[5]}");

        JsonElement recent = body.GetProperty("targetHistory").GetProperty("recent");
        recent.EnumerateArray().Select(item => item.GetProperty("id").GetGuid()).Should().Equal(ids[1], ids[2], ids[3], ids[4], ids[5]);
        recent[0].TryGetProperty("value", out _).Should().BeFalse();
    }

    [Fact]
    public async Task EntryWithoutOtherEntries_HasAHistoryOfItselfOnly()
    {
        User administrator = await AddAdministratorAsync();
        Guid entryId = await AddEntryAsync(administrator.Id, Time, AuditAction.PostEdited, AuditTargetType.Post, TargetId);

        JsonElement body = await GetJsonAsync($"{Path}/{entryId}");

        JsonElement history = body.GetProperty("targetHistory");
        history.GetProperty("totalCount").GetInt32().Should().Be(1);
        history.GetProperty("recent").EnumerateArray().Select(item => item.GetProperty("id").GetGuid()).Should().Equal(entryId);
    }

    [Fact]
    public async Task Change_HasWhenTheSameFieldLastChangedBefore()
    {
        User administrator = await AddAdministratorAsync();
        await AddEntryAsync(
            administrator.Id, Time, AuditAction.FeedbackStatusChanged, AuditTargetType.Feedback, TargetId,
            changes: [new AuditChange("status", "new", "wont_fix")]);
        await AddEntryAsync(
            administrator.Id, Time.AddDays(1), AuditAction.FeedbackStatusChanged, AuditTargetType.Feedback, OtherTargetId,
            changes: [new AuditChange("status", "new", "resolved")]);
        Guid entryId = await AddEntryAsync(
            administrator.Id, Time.AddDays(2), AuditAction.FeedbackStatusChanged, AuditTargetType.Feedback, TargetId,
            changes: [new AuditChange("status", "wont_fix", "resolved"), new AuditChange("other-field")]);

        JsonElement body = await GetJsonAsync($"{Path}/{entryId}");

        JsonElement[] changes = body.GetProperty("changes").EnumerateArray().ToArray();
        changes[0].GetProperty("previousChangedAt").GetDateTimeOffset().Should().Be(Time);
        changes[1].TryGetProperty("previousChangedAt", out _).Should().BeFalse();
    }

    [Fact]
    public async Task OldEntryOfARequest_HasNoSummaryNoRolesAndNoPreviousChange()
    {
        User administrator = await AddAdministratorAsync();
        Guid entryId = await AddEntryAsync(administrator.Id, Time, AuditAction.AccessDenied, AuditTargetType.Request, administrator.Id);

        JsonElement body = await GetJsonAsync($"{Path}/{entryId}");

        body.TryGetProperty("targetSummary", out _).Should().BeFalse();
        body.GetProperty("affectedUserRoles").GetArrayLength().Should().Be(0);
        body.GetProperty("changes").GetArrayLength().Should().Be(0);
        body.GetProperty("actor").GetProperty("username").GetString().Should().Be("username");
    }

    [Fact]
    public async Task ListOfFiftyEntries_RunsAsManyQueriesAsAListOfOne()
    {
        User administrator = await AddAdministratorAsync();
        await AddEntryAsync(administrator.Id, Time, AuditAction.PostEdited, AuditTargetType.Post, TargetId);
        int oneEntryQueries = await CountQueriesAsync($"{Path}?pageSize=50");
        for (int index = 1; index < 50; index++)
        {
            User affected = await AddUserAsync($"identity-id-{index + 1}", $"username-{index + 1}");
            await AddEntryAsync(
                administrator.Id, Time.AddMinutes(index), AuditAction.PostDeleted, AuditTargetType.Post, new Guid($"00000000-0000-0000-0000-{index:D12}"), affected.Id);
        }

        int fiftyEntryQueries = await CountQueriesAsync($"{Path}?pageSize=50");

        fiftyEntryQueries.Should().Be(oneEntryQueries).And.BeLessThanOrEqualTo(8);
    }

    [Fact]
    public async Task List_CarriesTheAffectedUserRolesButNotTheTargetContext()
    {
        User administrator = await AddAdministratorAsync();
        await AddEntryAsync(
            administrator.Id, Time, AuditAction.RoleAssigned, AuditTargetType.User, TargetId, TargetId,
            affectedUserRoles: ["Administrator", "Member"]);

        JsonElement body = await GetJsonAsync(Path);

        JsonElement item = body.GetProperty("items")[0];
        item.GetProperty("affectedUserRoles").EnumerateArray().Select(role => role.GetString()).Should().Equal("Administrator", "Member");
        item.TryGetProperty("targetSummary", out _).Should().BeFalse();
        item.TryGetProperty("targetHistory", out _).Should().BeFalse();
    }

    [Fact]
    public async Task Entry_RunsAFixedNumberOfQueries()
    {
        User administrator = await AddAdministratorAsync();
        User author = await AddUserAsync("identity-id-2", "username-2");
        Post post = await AddPostAsync(author);
        Guid entryId = Guid.Empty;
        for (int index = 0; index < 10; index++)
        {
            entryId = await AddEntryAsync(
                administrator.Id, Time.AddDays(index), AuditAction.PostEdited, AuditTargetType.Post, post.Id, author.Id,
                changes: [new AuditChange("content")]);
        }

        int queries = await CountQueriesAsync($"{Path}/{entryId}");

        queries.Should().BeLessThanOrEqualTo(12);
    }

    private async Task<User> AddAdministratorAsync()
    {
        return await AddUserAsync("identity-id", "username", await GetAdministratorRoleIdAsync());
    }

    private Task AddAsync(object entity)
    {
        return Factory.ExecuteDbContextAsync(dbContext =>
        {
            dbContext.Add(entity);
            return dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
    }

    private async Task<Guid> AddEntryAsync(
        Guid actorId,
        DateTimeOffset occurredAt,
        AuditAction action,
        AuditTargetType targetType,
        Guid targetId,
        Guid? affectedUserId = null,
        IReadOnlyList<AuditChange>? changes = null,
        IReadOnlyList<string>? affectedUserRoles = null)
    {
        AuditLogEntry entry = AuditLogEntry.Create(
            occurredAt,
            AuditActorType.User,
            actorId,
            ["Administrator"],
            "https://client.example.com",
            action,
            targetType,
            targetId,
            affectedUserId,
            null,
            "trace-id",
            "user-agent",
            System.Net.IPAddress.Parse("192.0.2.1"),
            changes,
            affectedUserRoles);

        await AddAsync(entry);

        return entry.Id;
    }

    private async Task<int> CountQueriesAsync(string path)
    {
        Factory.TimeProvider.Advance(TimeSpan.FromMinutes(GetAuditLogsQuery.RecordedReadWindowMinutes + 1));
        using HttpRequestMessage request = Request(HttpMethod.Get, path);
        Factory.DbCommands.Reset();

        using HttpResponseMessage response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return Factory.DbCommands.Count;
    }

    private async Task<JsonElement> GetJsonAsync(string path)
    {
        using HttpRequestMessage request = Request(HttpMethod.Get, path);
        using HttpResponseMessage response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        return await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
    }
}
