using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Choco.Domain.FeedbackEntries;
using Choco.Domain.Users;
using Choco.IntegrationTests.Notifications;

namespace Choco.IntegrationTests.Feedback;

public sealed class GetMyFeedbackTests : IClassFixture<ChocoApiFactory>, IAsyncLifetime
{
    private const string Path = "/api/v1/me/feedback";
    private static readonly FeedbackContext Context = new("/path", FeedbackLocale.Ja, FeedbackTheme.Dark, "user-agent", 1280, 720, "app-version");
    private readonly ChocoApiFactory _factory;
    private readonly HttpClient _client;

    public GetMyFeedbackTests(ChocoApiFactory factory)
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
        using HttpResponseMessage response = await _client.GetAsync(Path, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ReturnsOnlyTheCallerFeedback_NewestFirst_WithoutContext()
    {
        User caller = await NotificationTestData.AddUserAsync(_factory, "identity-id", "username");
        User other = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "username-2");
        FeedbackEntry first = await AddFeedbackAsync(caller, FeedbackCategory.Bug, "first message", FeedbackStatus.Resolved);
        _factory.TimeProvider.Advance(TimeSpan.FromHours(1));
        await AddFeedbackAsync(other, FeedbackCategory.Other, "other message");
        _factory.TimeProvider.Advance(TimeSpan.FromHours(1));
        FeedbackEntry second = await AddFeedbackAsync(caller, FeedbackCategory.Suggestion, "second message");

        JsonElement body = await GetAsync(Path);

        body.EnumerateObject().Select(property => property.Name).Should().BeEquivalentTo("items", "nextCursor");
        body.GetProperty("nextCursor").ValueKind.Should().Be(JsonValueKind.Null);
        JsonElement[] items = body.GetProperty("items").EnumerateArray().ToArray();
        items.Select(item => item.GetProperty("id").GetGuid()).Should().Equal(second.Id, first.Id);
        items[1].EnumerateObject().Select(property => property.Name).Should().BeEquivalentTo(
            "id", "category", "message", "status", "createdAt");
        items[1].GetProperty("category").GetString().Should().Be("bug");
        items[1].GetProperty("message").GetString().Should().Be("first message");
        items[1].GetProperty("status").GetString().Should().Be("resolved");
        items[1].GetProperty("createdAt").GetDateTimeOffset().Should().Be(first.CreatedAt);
    }

    [Fact]
    public async Task WithoutFeedback_ReturnsAnEmptyPage()
    {
        await NotificationTestData.AddUserAsync(_factory, "identity-id", "username");

        JsonElement body = await GetAsync(Path);

        body.GetProperty("items").EnumerateArray().Should().BeEmpty();
        body.GetProperty("nextCursor").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task PagesByCursor_WithoutSkippingFeedbackSentAtTheSameInstant()
    {
        User caller = await NotificationTestData.AddUserAsync(_factory, "identity-id", "username");
        List<FeedbackEntry> entries = [];
        for (int index = 0; index < 3; index++)
        {
            entries.Add(await AddFeedbackAsync(caller, FeedbackCategory.Bug, "feedback message"));
        }

        JsonElement firstPage = await GetAsync($"{Path}?limit=2");
        string cursor = firstPage.GetProperty("nextCursor").GetString()!;
        JsonElement secondPage = await GetAsync($"{Path}?limit=2&cursor={cursor}");

        Guid[] seen = firstPage.GetProperty("items").EnumerateArray()
            .Concat(secondPage.GetProperty("items").EnumerateArray())
            .Select(item => item.GetProperty("id").GetGuid())
            .ToArray();
        seen.Should().BeEquivalentTo(entries.Select(entry => entry.Id));
        seen.Should().OnlyHaveUniqueItems();
        secondPage.GetProperty("items").GetArrayLength().Should().Be(1);
        secondPage.GetProperty("nextCursor").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task SuspendedCaller_CanStillRead()
    {
        User caller = User.Create("identity-id", "username", "Display Name", null);
        caller.Suspend("reason", caller.Id, ChocoApiFactory.StartTime);
        await _factory.ExecuteDbContextAsync(db =>
        {
            db.Add(caller);
            return db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        await AddFeedbackAsync(caller, FeedbackCategory.Bug, "feedback message");

        JsonElement body = await GetAsync(Path);

        body.GetProperty("items").GetArrayLength().Should().Be(1);
    }

    [Theory]
    [InlineData("limit=0")]
    [InlineData("limit=51")]
    [InlineData("cursor=not-a-cursor")]
    public async Task InvalidParameters_ReturnBadRequest(string query)
    {
        await NotificationTestData.AddUserAsync(_factory, "identity-id", "username");
        using HttpRequestMessage request = NotificationTestData.CreateRequest(HttpMethod.Get, $"{Path}?{query}");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private async Task<JsonElement> GetAsync(string uri)
    {
        using HttpRequestMessage request = NotificationTestData.CreateRequest(HttpMethod.Get, uri);
        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
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
