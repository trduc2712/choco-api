using System.Net;
using System.Text.Json;

using Choco.Contract.Feedback;
using Choco.IntegrationTests.Versioning;

namespace Choco.IntegrationTests.Feedback;

public sealed class FeedbackOpenApiTests : IClassFixture<VersioningApiFactory>
{
    private readonly HttpClient _client;

    public FeedbackOpenApiTests(VersioningApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Theory]
    [InlineData("CreateFeedbackCommand", "category")]
    [InlineData("FeedbackResponse", "category")]
    [InlineData("FeedbackResponse", "status")]
    [InlineData("FeedbackSummaryResponse", "category")]
    [InlineData("FeedbackSummaryResponse", "status")]
    [InlineData("MyFeedbackResponse", "category")]
    [InlineData("MyFeedbackResponse", "status")]
    [InlineData("UpdateFeedbackStatusCommand", "status")]
    [InlineData("CreateFeedbackContextDto", "locale")]
    [InlineData("CreateFeedbackContextDto", "theme")]
    [InlineData("FeedbackContextDto", "locale")]
    [InlineData("FeedbackContextDto", "theme")]
    public async Task OpenApi_ListsTheAllowedValuesOfAFeedbackProperty(string schemaName, string property)
    {
        JsonElement schema = (await GetDocumentAsync()).GetProperty("components").GetProperty("schemas").GetProperty(schemaName);

        string[] values = schema.GetProperty("properties").GetProperty(property).GetProperty("enum")
            .EnumerateArray().Select(value => value.GetString()!).ToArray();

        values.Should().BeEquivalentTo(ExpectedValues(property));
    }

    [Theory]
    [InlineData("status")]
    [InlineData("category")]
    public async Task OpenApi_ListsTheAllowedValuesOfTheAdminListFilters(string name)
    {
        JsonElement operation = (await GetDocumentAsync()).GetProperty("paths").GetProperty("/api/v1/admin/feedback").GetProperty("get");

        JsonElement parameter = operation.GetProperty("parameters").EnumerateArray()
            .Single(candidate => candidate.GetProperty("name").GetString() == name);

        parameter.GetProperty("schema").GetProperty("enum").EnumerateArray().Select(value => value.GetString())
            .Should().BeEquivalentTo(ExpectedValues(name));
    }

    [Fact]
    public async Task OpenApi_DescribesTheAuthorAvatar()
    {
        JsonElement author = (await GetDocumentAsync()).GetProperty("components").GetProperty("schemas").GetProperty("FeedbackAuthorResponse");

        author.GetProperty("properties").EnumerateObject().Select(property => property.Name)
            .Should().BeEquivalentTo("id", "username", "displayName", "avatarUrl");
        JsonElement avatarUrl = author.GetProperty("properties").GetProperty("avatarUrl");
        avatarUrl.GetProperty("description").GetString().Should().NotBeNullOrWhiteSpace();
        avatarUrl.GetProperty("type").ToString().Should().Contain("string").And.Contain("null");
    }

    [Fact]
    public async Task OpenApi_DoesNotLetTheBodyCarryAUserAgent()
    {
        JsonElement schema = (await GetDocumentAsync()).GetProperty("components").GetProperty("schemas").GetProperty("CreateFeedbackCommand");

        schema.GetProperty("properties").EnumerateObject().Select(property => property.Name)
            .Should().BeEquivalentTo("category", "message", "context");
    }

    [Fact]
    public async Task OpenApi_DescribesTheNewAndChangedFeedbackOperations()
    {
        JsonElement paths = (await GetDocumentAsync()).GetProperty("paths");

        paths.TryGetProperty("/api/v1/admin/feedback/{id}", out JsonElement byId).Should().BeTrue();
        byId.TryGetProperty("patch", out _).Should().BeFalse();
        JsonElement status = paths.GetProperty("/api/v1/admin/feedback/{id}/status").GetProperty("put");
        status.GetProperty("responses").EnumerateObject().Select(response => response.Name)
            .Should().Contain(["200", "400", "401", "403", "404", "409"]);
        JsonElement mine = paths.GetProperty("/api/v1/me/feedback").GetProperty("get");
        mine.GetProperty("summary").GetString().Should().NotBeNullOrWhiteSpace();
        mine.GetProperty("responses").EnumerateObject().Select(response => response.Name)
            .Should().BeEquivalentTo(["200", "400", "401"]);
    }

    private static IReadOnlyList<string> ExpectedValues(string property)
    {
        return property switch
        {
            "category" => FeedbackWireValues.Categories,
            "status" => FeedbackWireValues.Statuses,
            "locale" => FeedbackWireValues.Locales,
            "theme" => FeedbackWireValues.Themes,
            _ => throw new ArgumentOutOfRangeException(nameof(property)),
        };
    }

    private async Task<JsonElement> GetDocumentAsync()
    {
        using HttpResponseMessage response = await _client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument document = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken),
            cancellationToken: TestContext.Current.CancellationToken);

        return document.RootElement.Clone();
    }
}
