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

public sealed class EditPostTests : IClassFixture<ChocoApiFactory>, IAsyncLifetime
{
    private static readonly Guid UnknownPostId = new("00000000-0000-0000-0000-000000000001");

    private readonly ChocoApiFactory _factory;
    private readonly HttpClient _client;

    public EditPostTests(ChocoApiFactory factory)
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
        User author = await AddUserAsync("identity-id", "username");
        Post post = await AddPostAsync(author);
        using HttpRequestMessage request = new(HttpMethod.Put, $"/api/posts/{post.Id}")
        {
            Content = JsonContent.Create(new { content = "edited-content" })
        };

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Author_EditsPostAndReturnsIt()
    {
        User author = await AddUserAsync("identity-id", "username");
        Post post = await AddPostAsync(author);
        _factory.TimeProvider.Advance(TimeSpan.FromMinutes(1));
        using HttpRequestMessage request = CreateRequest(post.Id, new { content = "  edited-content  " });

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        PostBody body = (await response.Content.ReadFromJsonAsync<PostBody>(TestContext.Current.CancellationToken))!;
        DateTimeOffset editedAt = ChocoApiFactory.StartTime.AddMinutes(1);
        body.Should().Be(new PostBody(post.Id, author.Id, "edited-content", ChocoApiFactory.StartTime, editedAt));
        Post saved = await GetPostAsync(post.Id);
        saved.Content.Should().Be("edited-content");
        saved.ModifiedAt.Should().Be(editedAt);
    }

    [Fact]
    public async Task AnotherUser_ReturnsForbiddenAndKeepsPost()
    {
        User author = await AddUserAsync("identity-id-2", "username-2");
        await AddUserAsync("identity-id", "username");
        Post post = await AddPostAsync(author);
        using HttpRequestMessage request = CreateRequest(post.Id, new { content = "edited-content" });

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await ShouldBeProblemAsync(response, HttpStatusCode.Forbidden, "Post.NotAuthor");
        (await GetPostAsync(post.Id)).Content.Should().Be("content");
    }

    [Fact]
    public async Task BlankContent_ReturnsValidationProblem()
    {
        User author = await AddUserAsync("identity-id", "username");
        Post post = await AddPostAsync(author);
        using HttpRequestMessage request = CreateRequest(post.Id, new { content = " " });

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        ValidationProblemDetails problem = (await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(TestContext.Current.CancellationToken))!;
        problem.Errors.Keys.Should().BeEquivalentTo(["Content"]);
        (await GetPostAsync(post.Id)).Content.Should().Be("content");
    }

    [Fact]
    public async Task UnknownPost_ReturnsNotFound()
    {
        await AddUserAsync("identity-id", "username");
        using HttpRequestMessage request = CreateRequest(UnknownPostId, new { content = "edited-content" });

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await ShouldBeProblemAsync(response, HttpStatusCode.NotFound, "Post.NotFound");
    }

    [Fact]
    public async Task DeletedPost_ReturnsNotFound()
    {
        User author = await AddUserAsync("identity-id", "username");
        Post post = await AddPostAsync(author);
        await _factory.ExecuteDbContextAsync(dbContext =>
        {
            dbContext.Remove(dbContext.Set<Post>().Single(p => p.Id == post.Id));
            return dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        using HttpRequestMessage request = CreateRequest(post.Id, new { content = "edited-content" });

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await ShouldBeProblemAsync(response, HttpStatusCode.NotFound, "Post.NotFound");
    }

    private static HttpRequestMessage CreateRequest(Guid id, object body)
    {
        HttpRequestMessage request = new(HttpMethod.Put, $"/api/posts/{id}")
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TestJwt.Create(DateTimeOffset.UtcNow));

        return request;
    }

    private static async Task ShouldBeProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        response.StatusCode.Should().Be(status);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        ProblemDetails problem = (await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken))!;
        problem.Extensions["code"].Should().BeOfType<JsonElement>().Which.GetString().Should().Be(code);
    }

    private async Task<User> AddUserAsync(string identityId, string username)
    {
        User user = User.Create(identityId, username, "Display Name", null);
        await _factory.ExecuteDbContextAsync(dbContext =>
        {
            dbContext.Add(user);
            return dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        return user;
    }

    private async Task<Post> AddPostAsync(User author)
    {
        Post post = Post.Create("content", author.Id);
        await _factory.ExecuteDbContextAsync(dbContext =>
        {
            dbContext.Add(post);
            return dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        return post;
    }

    private Task<Post> GetPostAsync(Guid id)
    {
        return _factory.ExecuteDbContextAsync(dbContext => dbContext.Set<Post>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync(p => p.Id == id, TestContext.Current.CancellationToken));
    }

    private sealed record PostBody(Guid Id, Guid AuthorId, string Content, DateTimeOffset CreatedAt, DateTimeOffset ModifiedAt);
}
