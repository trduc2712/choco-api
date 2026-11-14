using System.Net;
using System.Net.Http.Json;

using Choco.Contract.Abstractions;
using Choco.Contract.Feedback;
using Choco.Domain.FeedbackEntries;
using Choco.Domain.Roles;
using Choco.Domain.Users;
using Choco.IntegrationTests.Notifications;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Choco.IntegrationTests.Feedback;

public sealed class AdminFeedbackSearchTests : IClassFixture<ChocoApiFactory>, IAsyncLifetime
{
    private const string Path = "/api/v1/admin/feedback";
    private static readonly FeedbackContext Context = new("/path", FeedbackLocale.Ja, FeedbackTheme.Dark, "user-agent", 1280, 720, "app-version");
    private readonly ChocoApiFactory _factory;
    private readonly HttpClient _client;

    public AdminFeedbackSearchTests(ChocoApiFactory factory)
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
    public async Task Searching_ByMessageWords_RequiresEveryWordAnywhereInTheMessage()
    {
        await AddAdminAsync("identity-id", "username");
        User author = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "username-2");
        FeedbackEntry both = await AddFeedbackAsync(author, FeedbackCategory.Bug, "alpha and beta together");
        FeedbackEntry alphaOnly = await AddFeedbackAsync(author, FeedbackCategory.Bug, "only alpha here");
        await AddFeedbackAsync(author, FeedbackCategory.Bug, "nothing relevant");

        OffsetPageResponse<FeedbackSummaryResponse> single = await GetPageAsync($"{Path}?q=alpha");
        OffsetPageResponse<FeedbackSummaryResponse> multiple = await GetPageAsync($"{Path}?q=beta%20%20alpha");
        OffsetPageResponse<FeedbackSummaryResponse> none = await GetPageAsync($"{Path}?q=alpha%20missing");

        single.Items.Select(item => item.Id).Should().BeEquivalentTo([both.Id, alphaOnly.Id]);
        single.TotalCount.Should().Be(2);
        multiple.Items.Select(item => item.Id).Should().Equal(both.Id);
        multiple.TotalCount.Should().Be(1);
        none.Items.Should().BeEmpty();
        none.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Searching_ByMessageWords_UsesOnlyTheFirstFiveWords()
    {
        await AddAdminAsync("identity-id", "username");
        User author = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "username-2");
        await AddFeedbackAsync(author, FeedbackCategory.Bug, "one two three four five");

        OffsetPageResponse<FeedbackSummaryResponse> page = await GetPageAsync($"{Path}?q=one%20two%20three%20four%20five%20missing");

        page.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Searching_ByAuthor_MatchesPartOfUsernameOrDisplayName()
    {
        await AddAdminAsync("identity-id", "username");
        User byUsername = await AddUserAsync("identity-id-2", "username-needle", "Display Name 2");
        User byDisplayName = await AddUserAsync("identity-id-3", "username-3", "Display Needle Name");
        User other = await AddUserAsync("identity-id-4", "username-4", "Display Name 4");
        FeedbackEntry first = await AddFeedbackAsync(byUsername, FeedbackCategory.Bug, "feedback message");
        FeedbackEntry second = await AddFeedbackAsync(byDisplayName, FeedbackCategory.Bug, "feedback message");
        await AddFeedbackAsync(other, FeedbackCategory.Bug, "feedback message");

        OffsetPageResponse<FeedbackSummaryResponse> partial = await GetPageAsync($"{Path}?author=eedl");
        OffsetPageResponse<FeedbackSummaryResponse> phrase = await GetPageAsync($"{Path}?author=Display%20Needle");
        OffsetPageResponse<FeedbackSummaryResponse> reversedPhrase = await GetPageAsync($"{Path}?author=Needle%20Display");

        partial.Items.Select(item => item.Id).Should().BeEquivalentTo([first.Id, second.Id]);
        partial.TotalCount.Should().Be(2);
        phrase.Items.Select(item => item.Id).Should().Equal(second.Id);
        reversedPhrase.Items.Should().BeEmpty();
    }

    [Theory]
    [InlineData("q", "NEEDLE")]
    [InlineData("q", "needle")]
    [InlineData("q", "néedle")]
    [InlineData("q", "ñeedle")]
    [InlineData("author", "NEEDLE")]
    [InlineData("author", "néedle")]
    public async Task Searching_IgnoresCaseAndAccents(string field, string text)
    {
        await AddAdminAsync("identity-id", "username");
        User author = await AddUserAsync("identity-id-2", "username-2", "Display Needle");
        FeedbackEntry entry = await AddFeedbackAsync(author, FeedbackCategory.Bug, "Message With Needle");

        OffsetPageResponse<FeedbackSummaryResponse> page = await GetPageAsync($"{Path}?{field}={Uri.EscapeDataString(text)}");

        page.Items.Select(item => item.Id).Should().Equal(entry.Id);
    }

    [Theory]
    [InlineData("q", "d", "đ message text")]
    [InlineData("q", "D", "Đ message text")]
    [InlineData("q", "đ", "d message text")]
    [InlineData("q", "Đ", "D message text")]
    [InlineData("author", "d", "đ")]
    [InlineData("author", "Đ", "D")]
    public async Task Searching_TreatsStrokedDAsD(string field, string text, string stored)
    {
        await AddAdminAsync("identity-id", "username");
        User author = await AddUserAsync("identity-id-2", "username-2", field == "author" ? stored : "Display Name");
        FeedbackEntry entry = await AddFeedbackAsync(author, FeedbackCategory.Bug, field == "q" ? stored : "feedback message");

        OffsetPageResponse<FeedbackSummaryResponse> page = await GetPageAsync($"{Path}?{field}={Uri.EscapeDataString(text)}");

        page.Items.Select(item => item.Id).Should().Equal(entry.Id);
    }

    [Theory]
    [InlineData("50%", "discount 50% off")]
    [InlineData("snake_case", "name snake_case here")]
    [InlineData("back\\slash", "path back\\slash here")]
    public async Task Searching_TreatsLikeWildcardsAsPlainCharacters(string text, string matching)
    {
        await AddAdminAsync("identity-id", "username");
        User author = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "username-2");
        FeedbackEntry expected = await AddFeedbackAsync(author, FeedbackCategory.Bug, matching);
        await AddFeedbackAsync(author, FeedbackCategory.Bug, "discount 500 off");
        await AddFeedbackAsync(author, FeedbackCategory.Bug, "name snakeXcase here");
        await AddFeedbackAsync(author, FeedbackCategory.Bug, "path backXslash here");

        OffsetPageResponse<FeedbackSummaryResponse> page = await GetPageAsync($"{Path}?q={Uri.EscapeDataString(text)}");

        page.Items.Select(item => item.Id).Should().Equal(expected.Id);
        page.TotalCount.Should().Be(1);
    }

    [Theory]
    [InlineData("100%", "100% display")]
    [InlineData("under_score", "under_score display")]
    public async Task Searching_ByAuthor_TreatsLikeWildcardsAsPlainCharacters(string text, string displayName)
    {
        await AddAdminAsync("identity-id", "username");
        User expectedAuthor = await AddUserAsync("identity-id-2", "username-2", displayName);
        User otherAuthor = await AddUserAsync("identity-id-3", "username-3", "100 display under-score display");
        FeedbackEntry expected = await AddFeedbackAsync(expectedAuthor, FeedbackCategory.Bug, "feedback message");
        await AddFeedbackAsync(otherAuthor, FeedbackCategory.Bug, "feedback message");

        OffsetPageResponse<FeedbackSummaryResponse> page = await GetPageAsync($"{Path}?author={Uri.EscapeDataString(text)}");

        page.Items.Select(item => item.Id).Should().Equal(expected.Id);
    }

    [Fact]
    public async Task Searching_CombinesWordsAuthorStatusAndCategoryAndCountsTheMatches()
    {
        await AddAdminAsync("identity-id", "username");
        User wanted = await AddUserAsync("identity-id-2", "username-2", "Display Name 2");
        User other = await AddUserAsync("identity-id-3", "username-3", "Display Name 3");
        List<FeedbackEntry> matches = [];
        for (int index = 0; index < 3; index++)
        {
            matches.Add(await AddFeedbackAsync(wanted, FeedbackCategory.Bug, "needle message", FeedbackStatus.Resolved));
            _factory.TimeProvider.Advance(TimeSpan.FromHours(1));
        }

        await AddFeedbackAsync(wanted, FeedbackCategory.Bug, "needle message");
        await AddFeedbackAsync(wanted, FeedbackCategory.Other, "needle message", FeedbackStatus.Resolved);
        await AddFeedbackAsync(wanted, FeedbackCategory.Bug, "other message", FeedbackStatus.Resolved);
        await AddFeedbackAsync(other, FeedbackCategory.Bug, "needle message", FeedbackStatus.Resolved);

        const string query = "?q=needle&author=username-2&status=resolved&category=bug&pageSize=2";
        OffsetPageResponse<FeedbackSummaryResponse> first = await GetPageAsync(Path + query);
        OffsetPageResponse<FeedbackSummaryResponse> second = await GetPageAsync(Path + query + "&page=2");

        first.Items.Select(item => item.Id).Should().Equal(matches[2].Id, matches[1].Id);
        first.TotalCount.Should().Be(3);
        second.Items.Select(item => item.Id).Should().Equal(matches[0].Id);
        second.TotalCount.Should().Be(3);
    }

    [Theory]
    [InlineData("?q=")]
    [InlineData("?q=%20%20")]
    [InlineData("?author=")]
    [InlineData("?author=%20%20")]
    [InlineData("?q=%20&author=%20")]
    public async Task Searching_WithBlankText_IgnoresIt(string query)
    {
        await AddAdminAsync("identity-id", "username");
        User author = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "username-2");
        await AddFeedbackAsync(author, FeedbackCategory.Bug, "first message");
        await AddFeedbackAsync(author, FeedbackCategory.Bug, "second message");

        OffsetPageResponse<FeedbackSummaryResponse> page = await GetPageAsync(Path + query);

        page.TotalCount.Should().Be(2);
        page.Items.Should().HaveCount(2);
    }

    [Theory]
    [InlineData("q")]
    [InlineData("author")]
    public async Task Searching_WithTextAtTheLimit_IsAccepted(string field)
    {
        await AddAdminAsync("identity-id", "username");
        string text = new('a', GetFeedbackQuery.SearchMaxLength);
        string padded = $"  {text}  ";

        OffsetPageResponse<FeedbackSummaryResponse> exact = await GetPageAsync($"{Path}?{field}={text}");
        OffsetPageResponse<FeedbackSummaryResponse> trimmed = await GetPageAsync($"{Path}?{field}={Uri.EscapeDataString(padded)}");

        exact.TotalCount.Should().Be(0);
        trimmed.TotalCount.Should().Be(0);
    }

    [Theory]
    [InlineData("q")]
    [InlineData("author")]
    public async Task Searching_WithTextPastTheLimit_ReturnsValidationProblem(string field)
    {
        await AddAdminAsync("identity-id", "username");
        string text = new('a', GetFeedbackQuery.SearchMaxLength + 1);
        using HttpRequestMessage request = NotificationTestData.CreateRequest(HttpMethod.Get, $"{Path}?{field}={text}");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        ValidationProblemDetails problem = (await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(TestContext.Current.CancellationToken))!;
        problem.Errors.Keys.Should().ContainSingle().Which.Should().Be(field);
    }

    [Fact]
    public async Task Searching_ByAuthor_DoesNotMatchADeletedAccount()
    {
        await AddAdminAsync("identity-id", "username");
        User deleted = await AddUserAsync("identity-id-2", "username-deleted", "Display Name 2");
        User active = await AddUserAsync("identity-id-3", "username-active", "Display Name 3");
        await AddFeedbackAsync(deleted, FeedbackCategory.Bug, "needle message");
        FeedbackEntry kept = await AddFeedbackAsync(active, FeedbackCategory.Bug, "needle message");
        await _factory.ExecuteDbContextAsync(db =>
        {
            db.Remove(db.Set<User>().Single(user => user.Id == deleted.Id));
            return db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        OffsetPageResponse<FeedbackSummaryResponse> byAuthor = await GetPageAsync($"{Path}?author=username-");
        OffsetPageResponse<FeedbackSummaryResponse> byWords = await GetPageAsync($"{Path}?q=needle");

        byAuthor.Items.Select(item => item.Id).Should().Equal(kept.Id);
        byAuthor.TotalCount.Should().Be(1);
        byWords.TotalCount.Should().Be(2);
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
}
