using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using Choco.Domain.Users;
using Choco.IntegrationTests.Authentication;

using Microsoft.AspNetCore.Mvc;

namespace Choco.IntegrationTests.Versioning;

public sealed class ApiVersioningTests : IClassFixture<VersioningApiFactory>, IAsyncLifetime
{
    private readonly VersioningApiFactory _factory;
    private readonly HttpClient _client;

    public ApiVersioningTests(VersioningApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public async ValueTask InitializeAsync() => await _factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task VersionOne_ReturnsProfileAndSupportedVersions()
    {
        User user = User.Create("identity-id", "username", "Display Name", null);
        await _factory.ExecuteDbContextAsync(dbContext =>
        {
            dbContext.Add(user);
            return dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        using HttpRequestMessage request = CreateRequest("/api/v1/me/profile");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken), cancellationToken: TestContext.Current.CancellationToken);
        body.RootElement.GetProperty("id").GetGuid().Should().Be(user.Id);
        body.RootElement.GetProperty("username").GetString().Should().Be("username");
        response.Headers.GetValues("api-supported-versions").Should().ContainSingle().Which.Should().Be("1.0");
        _factory.IdentityProvider.Requests.Should().BeEmpty();
    }

    [Theory]
    [InlineData("GET", "/api/me/profile")]
    [InlineData("POST", "/api/posts")]
    [InlineData("GET", "/api/users/00000000-0000-0000-0000-000000000001")]
    [InlineData("GET", "/api/users/by-username/username")]
    public async Task MissingVersion_ReturnsNotFound(string method, string path)
    {
        using HttpRequestMessage request = CreateRequest(path);
        request.Method = new HttpMethod(method);

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        _factory.IdentityProvider.Requests.Should().BeEmpty();
    }

    [Theory]
    [InlineData("/api/v2/me/profile")]
    [InlineData("/api/v1.1/me/profile")]
    [InlineData("/api/v2/posts/00000000-0000-0000-0000-000000000001")]
    [InlineData("/api/v2/users/00000000-0000-0000-0000-000000000001")]
    [InlineData("/api/v2/users/by-username/username")]
    [InlineData("/api/vinvalid/me/profile")]
    public async Task UnsupportedVersion_ReturnsNotFoundProblem(string path)
    {
        using HttpRequestMessage request = CreateRequest(path);

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        ProblemDetails problem = (await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken))!;
        problem.Status.Should().Be(404);
        _factory.IdentityProvider.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task OpenApi_DescribesVersionOneRoutesAndContracts()
    {
        using HttpResponseMessage response = await _client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken), cancellationToken: TestContext.Current.CancellationToken);
        JsonElement root = document.RootElement;
        root.GetProperty("info").GetProperty("version").GetString().Should().Be("1.0");
        JsonElement paths = root.GetProperty("paths");
        paths.TryGetProperty("/api/v1/me/profile", out _).Should().BeTrue();
        paths.TryGetProperty("/api/v1/users/{userId}", out _).Should().BeTrue();
        paths.TryGetProperty("/api/posts", out _).Should().BeFalse();
        paths.TryGetProperty("/api/webhooks/clerk", out _).Should().BeFalse();
        JsonElement webhook = paths.GetProperty("/api/v1/webhooks/clerk").GetProperty("post");
        webhook.GetProperty("summary").GetString().Should().Be("Receives a Clerk webhook.");
        webhook.GetProperty("description").GetString().Should().Contain("signature");
        webhook.GetProperty("responses").EnumerateObject().Select(status => status.Name).Should().Contain(["200", "400", "401"]);
        webhook.TryGetProperty("security", out _).Should().BeFalse();
        paths.EnumerateObject().Should().NotContain(path => path.Name.Contains("{version}"));
        JsonElement createPost = paths.GetProperty("/api/v1/posts").GetProperty("post");
        createPost.GetProperty("summary").GetString().Should().Be("Publishes a post.");
        createPost.GetProperty("description").GetString().Should().Contain("Any signed-in user");
        createPost.GetProperty("responses").EnumerateObject().Select(status => status.Name).Should().Contain(["201", "400", "401"]);
        JsonElement content = root.GetProperty("components").GetProperty("schemas").GetProperty("CreatePostCommand").GetProperty("properties").GetProperty("content");
        content.GetProperty("description").GetString().Should().NotBeNullOrWhiteSpace();
        root.GetProperty("components").GetProperty("schemas").GetProperty("CreatePostCommand").GetProperty("example").GetProperty("content").GetString().Should().Be("content");
        JsonElement getPost = paths.GetProperty("/api/v1/posts/{id}").GetProperty("get");
        JsonElement id = getPost.GetProperty("parameters").EnumerateArray().Single(parameter => parameter.GetProperty("name").GetString() == "id");
        id.GetProperty("description").GetString().Should().NotBeNullOrWhiteSpace();
        id.TryGetProperty("example", out _).Should().BeTrue();
        JsonElement getByUsername = paths.GetProperty("/api/v1/users/by-username/{username}").GetProperty("get");
        getByUsername.GetProperty("summary").GetString().Should().Be("Gets a user's basic profile by username.");
        getByUsername.GetProperty("description").GetString().Should().Contain("case-sensitive");
        getByUsername.GetProperty("responses").EnumerateObject().Select(status => status.Name).Should().Contain(["200", "400", "401", "404"]);
        JsonElement username = getByUsername.GetProperty("parameters").EnumerateArray().Single(parameter => parameter.GetProperty("name").GetString() == "username");
        username.GetProperty("description").GetString().Should().Contain("exact username");
        username.GetProperty("example").GetString().Should().Be("username");
    }

    [Theory]
    [InlineData("followers")]
    [InlineData("following")]
    public async Task OpenApi_DescribesFollowLists(string list)
    {
        using HttpResponseMessage response = await _client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken), cancellationToken: TestContext.Current.CancellationToken);
        JsonElement operation = document.RootElement.GetProperty("paths").GetProperty($"/api/v1/users/{{userId}}/{list}").GetProperty("get");
        operation.GetProperty("summary").GetString().Should().NotBeNullOrWhiteSpace();
        operation.GetProperty("description").GetString().Should().Contain("nextCursor");
        operation.GetProperty("responses").EnumerateObject().Select(status => status.Name).Should().Contain(["200", "400", "401", "404"]);
        operation.GetProperty("parameters").EnumerateArray().Select(parameter => parameter.GetProperty("name").GetString())
            .Should().Contain(["userId", "cursor", "limit"]);
        operation.GetProperty("parameters").EnumerateArray()
            .Should().OnlyContain(parameter => !string.IsNullOrWhiteSpace(parameter.GetProperty("description").GetString()));
    }

    [Theory]
    [InlineData("/api/v1/me/notifications", "get", new[] { "200", "400", "401" })]
    [InlineData("/api/v1/me/notifications/unread-count", "get", new[] { "200", "401" })]
    [InlineData("/api/v1/me/notifications/{notificationId}/read", "put", new[] { "204", "400", "401", "404" })]
    [InlineData("/api/v1/me/notifications/read", "put", new[] { "204", "401" })]
    public async Task OpenApi_DescribesNotifications(string path, string method, string[] statuses)
    {
        using HttpResponseMessage response = await _client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken), cancellationToken: TestContext.Current.CancellationToken);
        JsonElement operation = document.RootElement.GetProperty("paths").GetProperty(path).GetProperty(method);
        operation.GetProperty("summary").GetString().Should().NotBeNullOrWhiteSpace();
        operation.GetProperty("description").GetString().Should().NotBeNullOrWhiteSpace();
        operation.GetProperty("responses").EnumerateObject().Select(status => status.Name).Should().BeEquivalentTo(statuses);
        IEnumerable<JsonElement> parameters = operation.TryGetProperty("parameters", out JsonElement found) ? found.EnumerateArray() : [];
        parameters.Should().OnlyContain(parameter => !string.IsNullOrWhiteSpace(parameter.GetProperty("description").GetString()));
    }

    [Fact]
    public async Task OpenApi_DescribesFollowStatusOnUserLists()
    {
        using HttpResponseMessage response = await _client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken), cancellationToken: TestContext.Current.CancellationToken);
        JsonElement item = document.RootElement.GetProperty("components").GetProperty("schemas").GetProperty("UserSearchItemResponse");
        item.GetProperty("properties").EnumerateObject().Select(property => property.Name)
            .Should().BeEquivalentTo("id", "username", "displayName", "avatarUrl", "isFollowedByMe");
        item.GetProperty("properties").GetProperty("isFollowedByMe").GetProperty("description").GetString().Should().Contain("caller");
        item.GetProperty("required").EnumerateArray().Select(name => name.GetString()).Should().Contain("isFollowedByMe");
        item.GetProperty("example").GetProperty("isFollowedByMe").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task OpenApi_DescribesPostCommentCount()
    {
        using HttpResponseMessage response = await _client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken), cancellationToken: TestContext.Current.CancellationToken);
        JsonElement post = document.RootElement.GetProperty("components").GetProperty("schemas").GetProperty("PostResponse");
        post.GetProperty("properties").GetProperty("commentCount").GetProperty("description").GetString().Should().NotBeNullOrWhiteSpace();
        post.GetProperty("required").EnumerateArray().Select(name => name.GetString()).Should().Contain("commentCount");
        post.GetProperty("example").GetProperty("commentCount").GetInt32().Should().Be(2);
    }

    [Theory]
    [InlineData("/api/v1/posts/{id}/comments", "post", "201,400,401,404")]
    [InlineData("/api/v1/posts/{id}/comments", "get", "200,400,401,404")]
    [InlineData("/api/v1/comments/{id}", "delete", "204,400,401,403,404")]
    public async Task OpenApi_DescribesCommentOperations(string path, string method, string statuses)
    {
        using HttpResponseMessage response = await _client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken), cancellationToken: TestContext.Current.CancellationToken);
        JsonElement root = document.RootElement;
        JsonElement operation = root.GetProperty("paths").GetProperty(path).GetProperty(method);
        operation.GetProperty("summary").GetString().Should().NotBeNullOrWhiteSpace();
        operation.GetProperty("description").GetString().Should().NotBeNullOrWhiteSpace();
        operation.GetProperty("responses").EnumerateObject().Select(status => status.Name).Should().Contain(statuses.Split(','));
        operation.GetProperty("parameters").EnumerateArray()
            .Should().OnlyContain(parameter => !string.IsNullOrWhiteSpace(parameter.GetProperty("description").GetString()));
        JsonElement schemas = root.GetProperty("components").GetProperty("schemas");
        schemas.GetProperty("CommentResponse").GetProperty("properties").EnumerateObject().Select(property => property.Name)
            .Should().BeEquivalentTo("id", "postId", "authorId", "author", "content", "createdAt");
        schemas.GetProperty("CreateCommentCommand").GetProperty("example").GetProperty("content").GetString().Should().Be("content");
    }

    [Theory]
    [InlineData("put")]
    [InlineData("delete")]
    public async Task OpenApi_DescribesPostLikeOperations(string method)
    {
        using HttpResponseMessage response = await _client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken), cancellationToken: TestContext.Current.CancellationToken);
        JsonElement root = document.RootElement;
        JsonElement operation = root.GetProperty("paths").GetProperty("/api/v1/posts/{id}/like").GetProperty(method);
        operation.GetProperty("summary").GetString().Should().NotBeNullOrWhiteSpace();
        operation.GetProperty("description").GetString().Should().Contain("changes nothing");
        operation.GetProperty("responses").EnumerateObject().Select(status => status.Name).Should().Contain(["204", "400", "401", "404"]);
        JsonElement id = operation.GetProperty("parameters").EnumerateArray().Single(parameter => parameter.GetProperty("name").GetString() == "id");
        id.GetProperty("description").GetString().Should().NotBeNullOrWhiteSpace();
        id.TryGetProperty("example", out _).Should().BeTrue();
        JsonElement post = root.GetProperty("components").GetProperty("schemas").GetProperty("PostResponse");
        post.GetProperty("properties").EnumerateObject().Select(property => property.Name).Should().Contain(["likeCount", "isLikedByMe"]);
        post.GetProperty("required").EnumerateArray().Select(name => name.GetString()).Should().Contain(["likeCount", "isLikedByMe"]);
        post.GetProperty("properties").GetProperty("likeCount").GetProperty("description").GetString().Should().NotBeNullOrWhiteSpace();
        post.GetProperty("properties").GetProperty("isLikedByMe").GetProperty("description").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task OpenApi_DescribesPostAuthor()
    {
        using HttpResponseMessage response = await _client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken), cancellationToken: TestContext.Current.CancellationToken);
        JsonElement schemas = document.RootElement.GetProperty("components").GetProperty("schemas");
        JsonElement post = schemas.GetProperty("PostResponse");
        post.GetProperty("properties").GetProperty("author").GetProperty("$ref").GetString().Should().EndWith("PostAuthorResponse");
        post.GetProperty("required").EnumerateArray().Select(name => name.GetString()).Should().Contain("author");
        post.GetProperty("example").GetProperty("author").GetProperty("username").GetString().Should().Be("username");
        JsonElement author = schemas.GetProperty("PostAuthorResponse").GetProperty("properties");
        author.EnumerateObject().Select(property => property.Name).Should().BeEquivalentTo("username", "displayName", "avatarUrl");
        author.EnumerateObject().Should().OnlyContain(property => !string.IsNullOrWhiteSpace(property.Value.GetProperty("description").GetString()));
    }

    [Fact]
    public async Task OpenApi_DescribesRecommendedFeed()
    {
        using HttpResponseMessage response = await _client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken), cancellationToken: TestContext.Current.CancellationToken);
        JsonElement operation = document.RootElement.GetProperty("paths").GetProperty("/api/v1/me/feed/recommended").GetProperty("get");
        operation.GetProperty("summary").GetString().Should().NotBeNullOrWhiteSpace();
        operation.GetProperty("description").GetString().Should().Contain("nextCursor");
        operation.GetProperty("responses").EnumerateObject().Select(status => status.Name).Should().Contain(["200", "400", "401"]);
        operation.GetProperty("parameters").EnumerateArray().Select(parameter => parameter.GetProperty("name").GetString())
            .Should().Contain(["cursor", "limit"]);
        operation.GetProperty("parameters").EnumerateArray()
            .Should().OnlyContain(parameter => !string.IsNullOrWhiteSpace(parameter.GetProperty("description").GetString()));
    }

    [Fact]
    public async Task OpenApi_DescribesFollowingFeed()
    {
        using HttpResponseMessage response = await _client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken), cancellationToken: TestContext.Current.CancellationToken);
        JsonElement operation = document.RootElement.GetProperty("paths").GetProperty("/api/v1/me/feed/following").GetProperty("get");
        operation.GetProperty("summary").GetString().Should().NotBeNullOrWhiteSpace();
        operation.GetProperty("description").GetString().Should().Contain("nextCursor");
        operation.GetProperty("responses").EnumerateObject().Select(status => status.Name).Should().Contain(["200", "400", "401"]);
        operation.GetProperty("parameters").EnumerateArray().Select(parameter => parameter.GetProperty("name").GetString())
            .Should().Contain(["cursor", "limit"]);
        operation.GetProperty("parameters").EnumerateArray()
            .Should().OnlyContain(parameter => !string.IsNullOrWhiteSpace(parameter.GetProperty("description").GetString()));
    }

    [Fact]
    public async Task SwaggerUi_ListsVersionOneDocument()
    {
        using HttpResponseMessage response = await _client.GetAsync("/swagger/index.js", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().Contain("/openapi/v1.json");
    }

    private static HttpRequestMessage CreateRequest(string path)
    {
        HttpRequestMessage request = new(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TestJwt.Create(DateTimeOffset.UtcNow));
        return request;
    }
}
