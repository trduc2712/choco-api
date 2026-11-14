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
    public async Task Listing_ReturnsFeedbackNewestFirstByPageWithAuthorAndContext()
    {
        await AddAdminAsync("identity-id", "username");
        User author = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "username-2");
        FeedbackEntry first = await AddFeedbackAsync(author, FeedbackCategory.Bug, "first message");
        _factory.TimeProvider.Advance(TimeSpan.FromHours(1));
        FeedbackEntry second = await AddFeedbackAsync(author, FeedbackCategory.Suggestion, "second message");
        _factory.TimeProvider.Advance(TimeSpan.FromHours(1));
        FeedbackEntry third = await AddFeedbackAsync(author, FeedbackCategory.Other, "third message");

        OffsetPageResponse<FeedbackSummaryResponse> firstPage = await GetPageAsync($"{Path}?pageSize=2");
        OffsetPageResponse<FeedbackSummaryResponse> secondPage = await GetPageAsync($"{Path}?pageSize=2&page=2");

        firstPage.Items.Select(item => item.Id).Should().Equal(third.Id, second.Id);
        firstPage.Page.Should().Be(1);
        firstPage.PageSize.Should().Be(2);
        firstPage.TotalCount.Should().Be(3);
        secondPage.Items.Select(item => item.Id).Should().Equal(first.Id);
        secondPage.Page.Should().Be(2);
        secondPage.PageSize.Should().Be(2);
        secondPage.TotalCount.Should().Be(3);
        FeedbackSummaryResponse item = secondPage.Items.Single();
        item.Author.Should().Be(new FeedbackAuthorResponse(author.Id, "username-2", "username-2"));
        item.Category.Should().Be("bug");
        item.MessagePreview.Should().Be("first message");
        item.Status.Should().Be("new");
        item.CreatedAt.Should().Be(first.CreatedAt);
    }

    [Fact]
    public async Task Listing_ReturnsOnlyTheSummaryFields()
    {
        await AddAdminAsync("identity-id", "username");
        User author = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "username-2");
        await AddFeedbackAsync(author, FeedbackCategory.Bug, "feedback message");
        using HttpRequestMessage request = NotificationTestData.CreateRequest(HttpMethod.Get, Path);

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        body.GetProperty("items")[0].EnumerateObject().Select(property => property.Name).Should().BeEquivalentTo(
            "id", "author", "category", "messagePreview", "status", "createdAt");
    }

    [Theory]
    [InlineData(FeedbackSummaryResponse.MessagePreviewMaxLength, false)]
    [InlineData(FeedbackSummaryResponse.MessagePreviewMaxLength + 1, true)]
    public async Task Listing_CutsALongMessageToAPreviewAndLeavesAShortOneWhole(int length, bool cut)
    {
        await AddAdminAsync("identity-id", "username");
        User author = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "username-2");
        string message = new('a', length);
        FeedbackEntry entry = await AddFeedbackAsync(author, FeedbackCategory.Bug, message);

        OffsetPageResponse<FeedbackSummaryResponse> page = await GetPageAsync(Path);

        page.Items.Single().MessagePreview.Should().Be(
            cut ? new string('a', FeedbackSummaryResponse.MessagePreviewMaxLength) + "…" : message);
        using HttpRequestMessage detail = NotificationTestData.CreateRequest(HttpMethod.Get, $"{Path}/{entry.Id}");
        using HttpResponseMessage detailResponse = await _client.SendAsync(detail, TestContext.Current.CancellationToken);
        FeedbackResponse body = (await detailResponse.Content.ReadFromJsonAsync<FeedbackResponse>(TestContext.Current.CancellationToken))!;
        body.Message.Should().Be(message);
    }

    [Fact]
    public async Task Listing_DoesNotSplitASurrogatePairWhenCuttingThePreview()
    {
        await AddAdminAsync("identity-id", "username");
        User author = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "username-2");
        string message = new string('a', FeedbackSummaryResponse.MessagePreviewMaxLength - 1) + "😀" + "tail";
        await AddFeedbackAsync(author, FeedbackCategory.Bug, message);

        OffsetPageResponse<FeedbackSummaryResponse> page = await GetPageAsync(Path);

        page.Items.Single().MessagePreview.Should().Be(
            new string('a', FeedbackSummaryResponse.MessagePreviewMaxLength - 1) + "…");
    }

    [Fact]
    public async Task ChangingStatus_WithTheOldPatchRoute_IsNotFoundAnymore()
    {
        await AddAdminAsync("identity-id", "username");
        User author = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "username-2");
        FeedbackEntry entry = await AddFeedbackAsync(author, FeedbackCategory.Bug, "feedback message");
        using HttpRequestMessage request = NotificationTestData.CreateRequest(HttpMethod.Patch, $"{Path}/{entry.Id}");
        request.Content = JsonContent.Create(new { status = "resolved" });

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed);
        (await GetEntriesAsync()).Single().Status.Should().Be(FeedbackStatus.New);
    }

    [Fact]
    public async Task Listing_WithoutQuery_UsesTheFirstPageOfTwentyAndCamelCaseNames()
    {
        await AddAdminAsync("identity-id", "username");
        User author = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "username-2");
        await AddFeedbackAsync(author, FeedbackCategory.Bug, "feedback message");
        using HttpRequestMessage request = NotificationTestData.CreateRequest(HttpMethod.Get, Path);

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
    public async Task Listing_ReturnsTheMiddleAndLastPagesAndAnEmptyPagePastTheEnd()
    {
        await AddAdminAsync("identity-id", "username");
        User author = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "username-2");
        List<FeedbackEntry> entries = [];
        for (int index = 0; index < 5; index++)
        {
            entries.Add(await AddFeedbackAsync(author, FeedbackCategory.Bug, "feedback message"));
            _factory.TimeProvider.Advance(TimeSpan.FromHours(1));
        }

        OffsetPageResponse<FeedbackSummaryResponse> middle = await GetPageAsync($"{Path}?pageSize=2&page=2");
        OffsetPageResponse<FeedbackSummaryResponse> last = await GetPageAsync($"{Path}?pageSize=2&page=3");
        OffsetPageResponse<FeedbackSummaryResponse> pastEnd = await GetPageAsync($"{Path}?pageSize=2&page=99");
        OffsetPageResponse<FeedbackSummaryResponse> hugePage = await GetPageAsync($"{Path}?pageSize=50&page={int.MaxValue}");

        middle.Items.Select(item => item.Id).Should().Equal(entries[2].Id, entries[1].Id);
        last.Items.Select(item => item.Id).Should().Equal(entries[0].Id);
        pastEnd.Items.Should().BeEmpty();
        pastEnd.Page.Should().Be(99);
        pastEnd.TotalCount.Should().Be(5);
        hugePage.Items.Should().BeEmpty();
        hugePage.TotalCount.Should().Be(5);
    }

    [Fact]
    public async Task Listing_AcceptsTheSmallestAndLargestPageSize()
    {
        await AddAdminAsync("identity-id", "username");
        User author = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "username-2");
        for (int index = 0; index < 3; index++)
        {
            await AddFeedbackAsync(author, FeedbackCategory.Bug, "feedback message");
            _factory.TimeProvider.Advance(TimeSpan.FromHours(1));
        }

        OffsetPageResponse<FeedbackSummaryResponse> smallest = await GetPageAsync($"{Path}?pageSize=1");
        OffsetPageResponse<FeedbackSummaryResponse> largest = await GetPageAsync($"{Path}?pageSize=50");

        smallest.Items.Should().ContainSingle();
        smallest.TotalCount.Should().Be(3);
        largest.Items.Should().HaveCount(3);
        largest.PageSize.Should().Be(50);
    }

    [Fact]
    public async Task Listing_OrdersEntriesCreatedAtTheSameInstantByIdDescendingOnEveryPage()
    {
        await AddAdminAsync("identity-id", "username");
        User author = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "username-2");
        List<FeedbackEntry> entries = [];
        for (int index = 0; index < 5; index++)
        {
            entries.Add(await AddFeedbackAsync(author, FeedbackCategory.Bug, "feedback message"));
        }

        List<Guid> expected = entries.Select(entry => entry.Id).OrderDescending().ToList();

        OffsetPageResponse<FeedbackSummaryResponse> firstPage = await GetPageAsync($"{Path}?pageSize=2&page=1");
        OffsetPageResponse<FeedbackSummaryResponse> secondPage = await GetPageAsync($"{Path}?pageSize=2&page=2");
        OffsetPageResponse<FeedbackSummaryResponse> thirdPage = await GetPageAsync($"{Path}?pageSize=2&page=3");
        OffsetPageResponse<FeedbackSummaryResponse> repeated = await GetPageAsync($"{Path}?pageSize=2&page=1");

        firstPage.Items.Concat(secondPage.Items).Concat(thirdPage.Items).Select(item => item.Id)
            .Should().Equal(expected);
        repeated.Items.Select(item => item.Id).Should().Equal(firstPage.Items.Select(item => item.Id));
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

        OffsetPageResponse<FeedbackSummaryResponse> resolved = await GetPageAsync($"{Path}?status=resolved");
        OffsetPageResponse<FeedbackSummaryResponse> bugs = await GetPageAsync($"{Path}?category=bug");
        OffsetPageResponse<FeedbackSummaryResponse> resolvedBugs = await GetPageAsync($"{Path}?status=resolved&category=bug");
        OffsetPageResponse<FeedbackSummaryResponse> inProgress = await GetPageAsync($"{Path}?status=in_progress");

        resolved.Items.Select(item => item.Id).Should().Equal(resolvedSuggestion.Id, resolvedBug.Id);
        resolved.TotalCount.Should().Be(2);
        bugs.Items.Select(item => item.Id).Should().Equal(resolvedBug.Id, newBug.Id);
        bugs.TotalCount.Should().Be(2);
        resolvedBugs.Items.Select(item => item.Id).Should().Equal(resolvedBug.Id);
        resolvedBugs.TotalCount.Should().Be(1);
        inProgress.Items.Should().BeEmpty();
        inProgress.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Listing_CountsAllMatchesAcrossPagesWhenFiltered()
    {
        await AddAdminAsync("identity-id", "username");
        User author = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "username-2");
        for (int index = 0; index < 3; index++)
        {
            await AddFeedbackAsync(author, FeedbackCategory.Bug, "bug message", FeedbackStatus.Resolved);
            _factory.TimeProvider.Advance(TimeSpan.FromHours(1));
        }

        await AddFeedbackAsync(author, FeedbackCategory.Other, "other message");

        OffsetPageResponse<FeedbackSummaryResponse> page = await GetPageAsync($"{Path}?status=resolved&category=bug&pageSize=2");

        page.Items.Should().HaveCount(2);
        page.TotalCount.Should().Be(3);
    }

    [Theory]
    [InlineData("?status=New", "status")]
    [InlineData("?status=unknown", "status")]
    [InlineData("?category=Bug", "category")]
    [InlineData("?category=unknown", "category")]
    [InlineData("?page=0", "page")]
    [InlineData("?page=-1", "page")]
    [InlineData("?pageSize=0", "pageSize")]
    [InlineData("?pageSize=51", "pageSize")]
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
    public async Task ChangingStatus_SavesItAndReturnsTheUpdatedFeedback(string status, FeedbackStatus expected)
    {
        await AddAdminAsync("identity-id", "username");
        User author = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "username-2");
        FeedbackEntry entry = await AddFeedbackAsync(author, FeedbackCategory.Bug, "feedback message", FeedbackStatus.Resolved);
        _factory.TimeProvider.Advance(TimeSpan.FromHours(1));
        using HttpRequestMessage request = Put($"{Path}/{entry.Id}/status", new { status });

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

    [Theory]
    [InlineData(FeedbackStatus.InProgress)]
    [InlineData(FeedbackStatus.Resolved)]
    [InlineData(FeedbackStatus.WontFix)]
    public async Task ChangingStatus_BackToNewAfterItLeftNew_ReturnsConflictAndChangesNothing(FeedbackStatus current)
    {
        await AddAdminAsync("identity-id", "username");
        User author = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "username-2");
        FeedbackEntry entry = await AddFeedbackAsync(author, FeedbackCategory.Bug, "feedback message", current);
        _factory.TimeProvider.Advance(TimeSpan.FromHours(1));
        using HttpRequestMessage request = Put($"{Path}/{entry.Id}/status", new { status = "new" });

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await AssertProblemAsync(response, HttpStatusCode.Conflict, "Feedback.CannotReturnToNew");
        FeedbackEntry stored = (await GetEntriesAsync()).Should().ContainSingle().Subject;
        stored.Status.Should().Be(current);
        stored.ModifiedAt.Should().Be(entry.ModifiedAt);
    }

    [Fact]
    public async Task ChangingStatusOfNewFeedbackToNew_ChangesNothing()
    {
        await AddAdminAsync("identity-id", "username");
        User author = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "username-2");
        FeedbackEntry entry = await AddFeedbackAsync(author, FeedbackCategory.Bug, "feedback message");
        using HttpRequestMessage request = Put($"{Path}/{entry.Id}/status", new { status = "new" });

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await GetEntriesAsync()).Should().ContainSingle().Which.Status.Should().Be(FeedbackStatus.New);
    }

    [Fact]
    public async Task ChangingStatusToTheSameStatus_ChangesNothing()
    {
        await AddAdminAsync("identity-id", "username");
        User author = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "username-2");
        FeedbackEntry entry = await AddFeedbackAsync(author, FeedbackCategory.Bug, "feedback message", FeedbackStatus.Resolved);
        _factory.TimeProvider.Advance(TimeSpan.FromHours(1));
        using HttpRequestMessage request = Put($"{Path}/{entry.Id}/status", new { status = "resolved" });

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
        using HttpRequestMessage request = Put($"{Path}/{entry.Id}/status", new { status });

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
        using HttpRequestMessage request = Put($"{Path}/{UnknownId}/status", new { status = "resolved" });

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "Feedback.NotFound");
    }

    [Theory]
    [InlineData("GET", "")]
    [InlineData("GET", "/00000000-0000-0000-0000-000000000001")]
    [InlineData("PUT", "/00000000-0000-0000-0000-000000000001/status")]
    public async Task WithoutThePermission_ReturnsForbiddenAndChangesNothing(string method, string suffix)
    {
        User author = await NotificationTestData.AddUserAsync(_factory, "identity-id", "username");
        FeedbackEntry entry = await AddFeedbackAsync(author, FeedbackCategory.Bug, "feedback message");
        string path = suffix.Length == 0 ? Path : $"{Path}/{entry.Id}{(method == "PUT" ? "/status" : "")}";
        using HttpRequestMessage request = NotificationTestData.CreateRequest(new HttpMethod(method), path);
        if (method == "PUT")
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
    [InlineData("PUT", "/api/v1/admin/feedback/00000000-0000-0000-0000-000000000001/status")]
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

    private static HttpRequestMessage Put(string path, object body)
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

    private async Task<OffsetPageResponse<FeedbackSummaryResponse>> GetPageAsync(string uri)
    {
        using HttpRequestMessage request = NotificationTestData.CreateRequest(HttpMethod.Get, uri);
        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        return (await response.Content.ReadFromJsonAsync<OffsetPageResponse<FeedbackSummaryResponse>>(TestContext.Current.CancellationToken))!;
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
