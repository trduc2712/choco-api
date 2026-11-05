using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using Choco.Domain.Posts;
using Choco.Domain.Users;
using Choco.IntegrationTests.Authentication;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Choco.IntegrationTests.Posts;

public sealed class CreatePostTests : IClassFixture<ChocoApiFactory>, IAsyncLifetime
{
    private readonly ChocoApiFactory _factory;
    private readonly HttpClient _client;

    public CreatePostTests(ChocoApiFactory factory)
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
        using HttpRequestMessage request = new(HttpMethod.Post, "/api/posts")
        {
            Content = JsonContent.Create(new { content = "content" })
        };

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await GetPostsAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task NewUser_CreatesUserAndPostAndReturnsCreated()
    {
        _factory.IdentityProvider.RespondWith(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new
            {
                id = "identity-id",
                username = "username",
                first_name = "first-name",
                last_name = "last-name",
                image_url = "https://example.com/avatar.png"
            })
        });
        using HttpRequestMessage request = CreateRequest(new { content = "content" });

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        PostBody body = (await response.Content.ReadFromJsonAsync<PostBody>(TestContext.Current.CancellationToken))!;
        User user = (await GetUsersAsync()).Should().ContainSingle().Subject;
        Post post = (await GetPostsAsync()).Should().ContainSingle().Subject;
        response.Headers.Location.Should().Be(new Uri(_client.BaseAddress!, $"/api/posts/{post.Id}"));
        body.Should().Be(new PostBody(post.Id, user.Id, "content", ChocoApiFactory.StartTime, ChocoApiFactory.StartTime));
        post.AuthorId.Should().Be(user.Id);
        post.Content.Should().Be("content");
        post.CreatedAt.Should().Be(ChocoApiFactory.StartTime);
        post.ModifiedAt.Should().Be(ChocoApiFactory.StartTime);
        post.IsDeleted.Should().BeFalse();
        _factory.IdentityProvider.Requests.Should().ContainSingle();
    }

    [Fact]
    public async Task BlankContent_ReturnsValidationProblemAndCreatesNoPost()
    {
        await AddUserAsync();
        using HttpRequestMessage request = CreateRequest(new { content = " " });

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        ValidationProblemDetails problem = (await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(TestContext.Current.CancellationToken))!;
        problem.Errors.Keys.Should().BeEquivalentTo(["Content"]);
        problem.Extensions["code"].Should().BeOfType<JsonElement>().Which.GetString().Should().Be("Validation.Failed");
        (await GetPostsAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task DeletedUser_ReturnsUnauthorizedAndCreatesNoPost()
    {
        User user = await AddUserAsync();
        await _factory.ExecuteDbContextAsync(dbContext =>
        {
            dbContext.Remove(dbContext.Set<User>().Single(u => u.Id == user.Id));
            return dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        using HttpRequestMessage request = CreateRequest(new { content = "content" });

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        ProblemDetails problem = (await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken))!;
        problem.Extensions["code"].Should().BeOfType<JsonElement>().Which.GetString().Should().Be("User.Deleted");
        (await GetPostsAsync()).Should().BeEmpty();
    }

    private static HttpRequestMessage CreateRequest(object body)
    {
        HttpRequestMessage request = new(HttpMethod.Post, "/api/posts")
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TestJwt.Create(DateTimeOffset.UtcNow));

        return request;
    }

    private async Task<User> AddUserAsync()
    {
        User user = User.Create("identity-id", "username", "Display Name", null);
        await _factory.ExecuteDbContextAsync(dbContext =>
        {
            dbContext.Add(user);
            return dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        return user;
    }

    private Task<List<User>> GetUsersAsync()
    {
        return _factory.ExecuteDbContextAsync(dbContext => dbContext.Set<User>()
            .AsNoTracking()
            .ToListAsync(TestContext.Current.CancellationToken));
    }

    private Task<List<Post>> GetPostsAsync()
    {
        return _factory.ExecuteDbContextAsync(dbContext => dbContext.Set<Post>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .ToListAsync(TestContext.Current.CancellationToken));
    }

    private sealed record PostBody(Guid Id, Guid AuthorId, string Content, DateTimeOffset CreatedAt, DateTimeOffset ModifiedAt);
}
