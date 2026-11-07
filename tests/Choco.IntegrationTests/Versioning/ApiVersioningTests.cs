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
