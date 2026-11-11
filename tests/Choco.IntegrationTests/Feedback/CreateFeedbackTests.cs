using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Choco.Contract.Feedback;
using Choco.Domain.FeedbackEntries;
using Choco.Domain.Users;
using Choco.IntegrationTests.Notifications;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Choco.IntegrationTests.Feedback;

public sealed class CreateFeedbackTests : IClassFixture<ChocoApiFactory>, IAsyncLifetime
{
    private const string Path = "/api/v1/feedback";
    private readonly ChocoApiFactory _factory;
    private readonly HttpClient _client;

    public CreateFeedbackTests(ChocoApiFactory factory)
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
    public async Task SendingFeedback_ReturnsCreatedAndStoresNewFeedbackFromTheCaller()
    {
        User caller = await NotificationTestData.AddUserAsync(_factory, "identity-id", "username");
        using HttpRequestMessage request = Post(Body(message: "  feedback message  "));

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        CreatedFeedbackResponse created = (await response.Content.ReadFromJsonAsync<CreatedFeedbackResponse>(TestContext.Current.CancellationToken))!;
        FeedbackEntry stored = (await GetEntriesAsync()).Should().ContainSingle().Subject;
        created.Id.Should().Be(stored.Id);
        created.CreatedAt.Should().Be(_factory.TimeProvider.GetUtcNow());
        stored.AuthorId.Should().Be(caller.Id);
        stored.Category.Should().Be(FeedbackCategory.Suggestion);
        stored.Message.Should().Be("feedback message");
        stored.Status.Should().Be(FeedbackStatus.New);
        stored.CreatedAt.Should().Be(_factory.TimeProvider.GetUtcNow());
        stored.Context.Should().Be(new FeedbackContext("/path", FeedbackLocale.Ja, FeedbackTheme.Dark, "user-agent", 1280, 720, "app-version"));
    }

    [Fact]
    public async Task SendingFeedbackWithAnAuthorInTheBody_IgnoresTheAuthor()
    {
        User caller = await NotificationTestData.AddUserAsync(_factory, "identity-id", "username");
        User other = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "username-2");
        using HttpRequestMessage request = Post(Body(extra: new Dictionary<string, object> { ["authorId"] = other.Id, ["author"] = new { id = other.Id } }));

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await GetEntriesAsync()).Should().ContainSingle().Which.AuthorId.Should().Be(caller.Id);
    }

    [Theory]
    [InlineData(10)]
    [InlineData(2000)]
    public async Task SendingAMessageAtTheLimits_ReturnsCreated(int length)
    {
        await NotificationTestData.AddUserAsync(_factory, "identity-id", "username");
        using HttpRequestMessage request = Post(Body(message: new string('m', length)));

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await GetEntriesAsync()).Should().ContainSingle().Which.Message.Should().HaveLength(length);
    }

    [Theory]
    [InlineData(9)]
    [InlineData(2001)]
    public async Task SendingAMessageOutsideTheLimits_ReturnsValidationProblemOnMessage(int length)
    {
        await NotificationTestData.AddUserAsync(_factory, "identity-id", "username");
        using HttpRequestMessage request = Post(Body(message: new string('m', length)));

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await AssertValidationAsync(response, "message");
        (await GetEntriesAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task SendingAMessageThatIsTooShortOnceTrimmed_ReturnsValidationProblemOnMessage()
    {
        await NotificationTestData.AddUserAsync(_factory, "identity-id", "username");
        using HttpRequestMessage request = Post(Body(message: "   short   "));

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await AssertValidationAsync(response, "message");
    }

    [Fact]
    public async Task SendingAnUnknownCategory_ReturnsValidationProblemOnCategory()
    {
        await NotificationTestData.AddUserAsync(_factory, "identity-id", "username");
        using HttpRequestMessage request = Post(Body(category: "unknown"));

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await AssertValidationAsync(response, "category");
    }

    [Theory]
    [InlineData("path", "context.path")]
    [InlineData("/path?query=value", "context.path")]
    [InlineData("/path#hash", "context.path")]
    public async Task SendingAPathThatIsNotAPathname_ReturnsValidationProblemOnPath(string path, string field)
    {
        await NotificationTestData.AddUserAsync(_factory, "identity-id", "username");
        using HttpRequestMessage request = Post(Body(path: path));

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await AssertValidationAsync(response, field);
    }

    [Fact]
    public async Task SendingLongContextValues_ReturnsValidationProblemOnEachField()
    {
        await NotificationTestData.AddUserAsync(_factory, "identity-id", "username");
        using HttpRequestMessage request = Post(Body(
            path: "/" + new string('p', FeedbackContext.PathMaxLength),
            userAgent: new string('u', FeedbackContext.UserAgentMaxLength + 1),
            appVersion: new string('a', FeedbackContext.AppVersionMaxLength + 1)));

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        ValidationProblemDetails problem = (await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(TestContext.Current.CancellationToken))!;
        problem.Errors.Keys.Should().BeEquivalentTo("context.path", "context.userAgent", "context.appVersion");
    }

    [Fact]
    public async Task SendingContextValuesAtTheLimits_ReturnsCreated()
    {
        await NotificationTestData.AddUserAsync(_factory, "identity-id", "username");
        using HttpRequestMessage request = Post(Body(
            path: "/" + new string('p', FeedbackContext.PathMaxLength - 1),
            userAgent: new string('u', FeedbackContext.UserAgentMaxLength),
            appVersion: new string('a', FeedbackContext.AppVersionMaxLength)));

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Theory]
    [InlineData("fr", "system", "context.locale")]
    [InlineData("en", "blue", "context.theme")]
    public async Task SendingAnUnknownLocaleOrTheme_ReturnsValidationProblemOnThatField(string locale, string theme, string field)
    {
        await NotificationTestData.AddUserAsync(_factory, "identity-id", "username");
        using HttpRequestMessage request = Post(Body(locale: locale, theme: theme));

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await AssertValidationAsync(response, field);
    }

    [Fact]
    public async Task SendingAViewportWithoutSize_ReturnsValidationProblemOnWidthAndHeight()
    {
        await NotificationTestData.AddUserAsync(_factory, "identity-id", "username");
        using HttpRequestMessage request = Post(Body(viewportWidth: 0, viewportHeight: 0));

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        ValidationProblemDetails problem = (await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(TestContext.Current.CancellationToken))!;
        problem.Errors.Keys.Should().BeEquivalentTo("context.viewport.width", "context.viewport.height");
    }

    [Fact]
    public async Task SendingAnEmptyBody_ReturnsBadRequestAndStoresNothing()
    {
        await NotificationTestData.AddUserAsync(_factory, "identity-id", "username");
        using HttpRequestMessage request = NotificationTestData.CreateRequest(HttpMethod.Post, Path);
        request.Content = JsonContent.Create(new { });

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await GetEntriesAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task NoToken_ReturnsUnauthorizedAndStoresNothing()
    {
        using HttpRequestMessage request = new(HttpMethod.Post, Path) { Content = JsonContent.Create(BodyObject()) };

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await GetEntriesAsync()).Should().BeEmpty();
    }

    private static HttpRequestMessage Post(object body)
    {
        HttpRequestMessage request = NotificationTestData.CreateRequest(HttpMethod.Post, Path);
        request.Content = JsonContent.Create(body);
        return request;
    }

    private static Dictionary<string, object> Body(
        string category = "suggestion",
        string message = "feedback message",
        string path = "/path",
        string locale = "ja",
        string theme = "dark",
        string userAgent = "user-agent",
        int viewportWidth = 1280,
        int viewportHeight = 720,
        string appVersion = "app-version",
        Dictionary<string, object>? extra = null)
    {
        Dictionary<string, object> body = new()
        {
            ["category"] = category,
            ["message"] = message,
            ["context"] = new
            {
                path,
                locale,
                theme,
                userAgent,
                viewport = new { width = viewportWidth, height = viewportHeight },
                appVersion
            }
        };

        foreach ((string key, object value) in extra ?? [])
        {
            body[key] = value;
        }

        return body;
    }

    private static object BodyObject() => Body();

    private static async Task AssertValidationAsync(HttpResponseMessage response, string field)
    {
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        ValidationProblemDetails problem = (await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(TestContext.Current.CancellationToken))!;
        problem.Errors.Keys.Should().ContainSingle().Which.Should().Be(field);
        ((JsonElement)problem.Extensions["code"]!).GetString().Should().Be("Validation.Failed");
        problem.Extensions.Should().ContainKey("traceId");
    }

    private Task<List<FeedbackEntry>> GetEntriesAsync()
    {
        return _factory.ExecuteDbContextAsync(db => db.Set<FeedbackEntry>().AsNoTracking()
            .OrderBy(entry => entry.CreatedAt)
            .ToListAsync(TestContext.Current.CancellationToken));
    }
}
