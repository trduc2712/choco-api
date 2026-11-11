using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Choco.Contract.Abstractions;
using Choco.Contract.Feedback;
using Choco.Domain.FeedbackEntries;
using Choco.Domain.Roles;
using Choco.Domain.Users;
using Choco.IntegrationTests.Notifications;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Choco.IntegrationTests.Feedback;

public sealed class AdminFeedbackTests : IClassFixture<ChocoApiFactory>, IAsyncLifetime
{
    private const string Path = "/api/v1/admin/feedback";
    private static readonly Guid UnknownId = new("00000000-0000-0000-0000-000000000001");
    private static readonly FeedbackContext Context = new("/path", FeedbackLocale.Ja, FeedbackTheme.Dark, "user-agent", 1280, 720, "app-version");
    private readonly ChocoApiFactory _factory;
    private readonly HttpClient _client;

    public AdminFeedbackTests(ChocoApiFactory factory)
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
    public async Task Listing_ReturnsFeedbackNewestFirstByCursorWithAuthorAndContext()
    {
        await AddAdminAsync("identity-id", "username");
        User author = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "username-2");
        FeedbackEntry first = await AddFeedbackAsync(author, FeedbackCategory.Bug, "first message");
        _factory.TimeProvider.Advance(TimeSpan.FromHours(1));
        FeedbackEntry second = await AddFeedbackAsync(author, FeedbackCategory.Suggestion, "second message");
        _factory.TimeProvider.Advance(TimeSpan.FromHours(1));
        FeedbackEntry third = await AddFeedbackAsync(author, FeedbackCategory.Other, "third message");

        PageResponse<FeedbackResponse> firstPage = await GetPageAsync($"{Path}?limit=2");
        PageResponse<FeedbackResponse> secondPage = await GetPageAsync($"{Path}?limit=2&cursor={firstPage.NextCursor}");

        firstPage.Items.Select(item => item.Id).Should().Equal(third.Id, second.Id);
        firstPage.NextCursor.Should().NotBeNull();
        secondPage.Items.Select(item => item.Id).Should().Equal(first.Id);
        secondPage.NextCursor.Should().BeNull();
        FeedbackResponse item = secondPage.Items.Single();
        item.Author.Should().Be(new FeedbackAuthorResponse(author.Id, "username-2", "username-2"));
        item.Category.Should().Be("bug");
        item.Message.Should().Be("first message");
        item.Status.Should().Be("new");
        item.Context.Should().Be(new FeedbackContextDto(
            "/path", "ja", "dark", "user-agent", new FeedbackViewportDto(1280, 720), "app-version"));
        item.CreatedAt.Should().Be(first.CreatedAt);
        item.UpdatedAt.Should().Be(first.ModifiedAt);
    }

    [Fact]
    public async Task Listing_FiltersByStatusAndCategory()
    {
        await AddAdminAsync("identity-id", "username");
        User author = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "username-2");
        FeedbackEntry newBug = await AddFeedbackAsync(author, FeedbackCategory.Bug, "new bug message");
        _factory.TimeProvider.Advance(TimeSpan.FromHours(1));
        FeedbackEntry resolvedBug = await AddFeedbackAsync(author, FeedbackCategory.Bug, "resolved bug message", FeedbackStatus.Resolved);
        _factory.TimeProvider.Advance(TimeSpan.FromHours(1));
        FeedbackEntry resolvedSuggestion = await AddFeedbackAsync(author, FeedbackCategory.Suggestion, "resolved suggestion message", FeedbackStatus.Resolved);

        PageResponse<FeedbackResponse> resolved = await GetPageAsync($"{Path}?status=resolved");
        PageResponse<FeedbackResponse> bugs = await GetPageAsync($"{Path}?category=bug");
        PageResponse<FeedbackResponse> resolvedBugs = await GetPageAsync($"{Path}?status=resolved&category=bug");
        PageResponse<FeedbackResponse> inProgress = await GetPageAsync($"{Path}?status=in_progress");

        resolved.Items.Select(item => item.Id).Should().Equal(resolvedSuggestion.Id, resolvedBug.Id);
        bugs.Items.Select(item => item.Id).Should().Equal(resolvedBug.Id, newBug.Id);
        resolvedBugs.Items.Select(item => item.Id).Should().Equal(resolvedBug.Id);
        inProgress.Items.Should().BeEmpty();
        inProgress.NextCursor.Should().BeNull();
    }

    [Theory]
    [InlineData("?status=New", "status")]
    [InlineData("?status=unknown", "status")]
    [InlineData("?category=Bug", "category")]
    [InlineData("?category=unknown", "category")]
    [InlineData("?cursor=invalid", "cursor")]
    [InlineData("?limit=0", "limit")]
    [InlineData("?limit=51", "limit")]
    public async Task Listing_WithInvalidQuery_ReturnsValidationProblem(string query, string field)
    {
        await AddAdminAsync("identity-id", "username");
        using HttpRequestMessage request = NotificationTestData.CreateRequest(HttpMethod.Get, Path + query);

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        ValidationProblemDetails problem = (await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(TestContext.Current.CancellationToken))!;
        problem.Errors.Keys.Should().ContainSingle().Which.Should().Be(field);
    }

    [Fact]
    public async Task Getting_ReturnsTheFeedback()
    {
        await AddAdminAsync("identity-id", "username");
        User author = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "username-2");
        FeedbackEntry entry = await AddFeedbackAsync(author, FeedbackCategory.Other, "feedback message", FeedbackStatus.WontFix);
        using HttpRequestMessage request = NotificationTestData.CreateRequest(HttpMethod.Get, $"{Path}/{entry.Id}");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        FeedbackResponse body = (await response.Content.ReadFromJsonAsync<FeedbackResponse>(TestContext.Current.CancellationToken))!;
        body.Id.Should().Be(entry.Id);
        body.Author!.Id.Should().Be(author.Id);
        body.Category.Should().Be("other");
        body.Status.Should().Be("wont_fix");
        body.Message.Should().Be("feedback message");
    }

    [Fact]
    public async Task Getting_UsesLowerSnakeCaseNamesOnTheWire()
    {
        await AddAdminAsync("identity-id", "username");
        User author = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "username-2");
        FeedbackEntry entry = await AddFeedbackAsync(author, FeedbackCategory.Bug, "feedback message", FeedbackStatus.InProgress);
        using HttpRequestMessage request = NotificationTestData.CreateRequest(HttpMethod.Get, $"{Path}/{entry.Id}");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        body.EnumerateObject().Select(property => property.Name).Should().BeEquivalentTo(
            "id", "author", "category", "message", "context", "status", "createdAt", "updatedAt");
        body.GetProperty("status").GetString().Should().Be("in_progress");
        body.GetProperty("context").EnumerateObject().Select(property => property.Name).Should().BeEquivalentTo(
            "path", "locale", "theme", "userAgent", "viewport", "appVersion");
    }

    [Fact]
    public async Task Getting_AnUnknownFeedback_ReturnsNotFound()
    {
        await AddAdminAsync("identity-id", "username");
        using HttpRequestMessage request = NotificationTestData.CreateRequest(HttpMethod.Get, $"{Path}/{UnknownId}");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "Feedback.NotFound");
    }

    [Fact]
    public async Task Getting_WithAnEmptyId_ReturnsValidationProblem()
    {
        await AddAdminAsync("identity-id", "username");
        using HttpRequestMessage request = NotificationTestData.CreateRequest(HttpMethod.Get, $"{Path}/{Guid.Empty}");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        ValidationProblemDetails problem = (await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(TestContext.Current.CancellationToken))!;
        problem.Errors.Keys.Should().ContainSingle().Which.Should().Be("id");
    }

    [Theory]
    [InlineData("in_progress", FeedbackStatus.InProgress)]
    [InlineData("resolved", FeedbackStatus.Resolved)]
    [InlineData("wont_fix", FeedbackStatus.WontFix)]
    [InlineData("new", FeedbackStatus.New)]
    public async Task ChangingStatus_SavesItAndReturnsTheUpdatedFeedback(string status, FeedbackStatus expected)
    {
        await AddAdminAsync("identity-id", "username");
        User author = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "username-2");
        FeedbackEntry entry = await AddFeedbackAsync(author, FeedbackCategory.Bug, "feedback message", FeedbackStatus.Resolved);
        _factory.TimeProvider.Advance(TimeSpan.FromHours(1));
        using HttpRequestMessage request = Patch($"{Path}/{entry.Id}", new { status });

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        FeedbackResponse body = (await response.Content.ReadFromJsonAsync<FeedbackResponse>(TestContext.Current.CancellationToken))!;
        body.Status.Should().Be(status);
        body.CreatedAt.Should().Be(entry.CreatedAt);
        FeedbackEntry stored = (await GetEntriesAsync()).Should().ContainSingle().Subject;
        stored.Status.Should().Be(expected);
        stored.Message.Should().Be("feedback message");
        stored.AuthorId.Should().Be(author.Id);
        if (expected != FeedbackStatus.Resolved)
        {
            stored.ModifiedAt.Should().Be(_factory.TimeProvider.GetUtcNow());
            body.UpdatedAt.Should().Be(stored.ModifiedAt);
        }
    }

    [Fact]
    public async Task ChangingStatusToTheSameStatus_ChangesNothing()
    {
        await AddAdminAsync("identity-id", "username");
        User author = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "username-2");
        FeedbackEntry entry = await AddFeedbackAsync(author, FeedbackCategory.Bug, "feedback message", FeedbackStatus.Resolved);
        _factory.TimeProvider.Advance(TimeSpan.FromHours(1));
        using HttpRequestMessage request = Patch($"{Path}/{entry.Id}", new { status = "resolved" });

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        FeedbackEntry stored = (await GetEntriesAsync()).Should().ContainSingle().Subject;
        stored.Status.Should().Be(FeedbackStatus.Resolved);
        stored.ModifiedAt.Should().Be(entry.ModifiedAt);
    }

    [Theory]
    [InlineData("InProgress")]
    [InlineData("unknown")]
    [InlineData("")]
    public async Task ChangingStatus_ToAnUnknownStatus_ReturnsValidationProblemAndKeepsTheStatus(string status)
    {
        await AddAdminAsync("identity-id", "username");
        User author = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "username-2");
        FeedbackEntry entry = await AddFeedbackAsync(author, FeedbackCategory.Bug, "feedback message");
        using HttpRequestMessage request = Patch($"{Path}/{entry.Id}", new { status });

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        ValidationProblemDetails problem = (await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(TestContext.Current.CancellationToken))!;
        problem.Errors.Keys.Should().ContainSingle().Which.Should().Be("status");
        (await GetEntriesAsync()).Should().ContainSingle().Which.Status.Should().Be(FeedbackStatus.New);
    }

    [Fact]
    public async Task ChangingStatus_OfAnUnknownFeedback_ReturnsNotFound()
    {
        await AddAdminAsync("identity-id", "username");
        using HttpRequestMessage request = Patch($"{Path}/{UnknownId}", new { status = "resolved" });

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "Feedback.NotFound");
    }

    [Theory]
    [InlineData("GET", "")]
    [InlineData("GET", "/00000000-0000-0000-0000-000000000001")]
    [InlineData("PATCH", "/00000000-0000-0000-0000-000000000001")]
    public async Task WithoutThePermission_ReturnsForbiddenAndChangesNothing(string method, string suffix)
    {
        User author = await NotificationTestData.AddUserAsync(_factory, "identity-id", "username");
        FeedbackEntry entry = await AddFeedbackAsync(author, FeedbackCategory.Bug, "feedback message");
        string path = suffix.Length == 0 ? Path : $"{Path}/{entry.Id}";
        using HttpRequestMessage request = NotificationTestData.CreateRequest(new HttpMethod(method), path);
        if (method == "PATCH")
        {
            request.Content = JsonContent.Create(new { status = "resolved" });
        }

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await AssertProblemAsync(response, HttpStatusCode.Forbidden, "Permission.Denied");
        (await GetEntriesAsync()).Should().ContainSingle().Which.Status.Should().Be(FeedbackStatus.New);
    }

    [Theory]
    [InlineData("GET", "/api/v1/admin/feedback")]
    [InlineData("GET", "/api/v1/admin/feedback/00000000-0000-0000-0000-000000000001")]
    [InlineData("PATCH", "/api/v1/admin/feedback/00000000-0000-0000-0000-000000000001")]
    public async Task NoToken_ReturnsUnauthorized(string method, string path)
    {
        using HttpRequestMessage request = new(new HttpMethod(method), path);

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AdminRole_HoldsBothFeedbackPermissions()
    {
        IReadOnlyCollection<string> permissions = await _factory.ExecuteDbContextAsync(async db =>
            (await db.Set<Role>().SingleAsync(role => role.SystemKey == SystemRoles.Admin, TestContext.Current.CancellationToken)).Permissions);

        permissions.Should().Contain([Permission.ReadAnyFeedback, Permission.ChangeFeedbackStatus]);
    }

    private static HttpRequestMessage Patch(string path, object body)
    {
        HttpRequestMessage request = NotificationTestData.CreateRequest(HttpMethod.Patch, path);
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

    private async Task<PageResponse<FeedbackResponse>> GetPageAsync(string uri)
    {
        using HttpRequestMessage request = NotificationTestData.CreateRequest(HttpMethod.Get, uri);
        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        return (await response.Content.ReadFromJsonAsync<PageResponse<FeedbackResponse>>(TestContext.Current.CancellationToken))!;
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

    private async Task<FeedbackEntry> AddFeedbackAsync(
        User author, FeedbackCategory category, string message, FeedbackStatus status = FeedbackStatus.New)
    {
        FeedbackEntry entry = FeedbackEntry.Create(author.Id, category, message, Context);
        entry.ChangeStatus(status);
        await _factory.ExecuteDbContextAsync(db =>
        {
            db.Add(entry);
            return db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        return entry;
    }

    private Task<List<FeedbackEntry>> GetEntriesAsync()
    {
        return _factory.ExecuteDbContextAsync(db => db.Set<FeedbackEntry>().AsNoTracking()
            .OrderBy(entry => entry.CreatedAt)
            .ToListAsync(TestContext.Current.CancellationToken));
    }
}
