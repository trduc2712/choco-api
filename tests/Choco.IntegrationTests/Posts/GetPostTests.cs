using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using Choco.Domain.Posts;
using Choco.Domain.Users;
using Choco.IntegrationTests.Authentication;

using Microsoft.AspNetCore.Mvc;

namespace Choco.IntegrationTests.Posts;

public sealed class GetPostTests : IClassFixture<ChocoApiFactory>, IAsyncLifetime
{
    private static readonly Guid UnknownPostId = new("00000000-0000-0000-0000-000000000001");

    private readonly ChocoApiFactory _factory;
    private readonly HttpClient _client;

    public GetPostTests(ChocoApiFactory factory)
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
        Post post = await AddPostAsync();
        using HttpRequestMessage request = new(HttpMethod.Get, $"/api/v1/posts/{post.Id}");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ExistingPost_ReturnsPost()
    {
        Post post = await AddPostAsync();
        using HttpRequestMessage request = CreateRequest(post.Id);

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        PostBody body = (await response.Content.ReadFromJsonAsync<PostBody>(TestContext.Current.CancellationToken))!;
        body.Should().Be(new PostBody(post.Id, post.AuthorId, "content", ChocoApiFactory.StartTime, ChocoApiFactory.StartTime));
    }

    [Fact]
    public async Task UnknownPost_ReturnsNotFound()
    {
        using HttpRequestMessage request = CreateRequest(UnknownPostId);

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await ShouldBePostNotFoundProblemAsync(response);
    }

    [Fact]
    public async Task DeletedPost_ReturnsNotFound()
    {
        Post post = await AddPostAsync();
        await _factory.ExecuteDbContextAsync(dbContext =>
        {
            dbContext.Remove(dbContext.Set<Post>().Single(p => p.Id == post.Id));
            return dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        using HttpRequestMessage request = CreateRequest(post.Id);

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await ShouldBePostNotFoundProblemAsync(response);
    }

    private static HttpRequestMessage CreateRequest(Guid id)
    {
        HttpRequestMessage request = new(HttpMethod.Get, $"/api/v1/posts/{id}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TestJwt.Create(DateTimeOffset.UtcNow));

        return request;
    }

    private static async Task ShouldBePostNotFoundProblemAsync(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        ProblemDetails problem = (await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken))!;
        problem.Extensions["code"].Should().BeOfType<JsonElement>().Which.GetString().Should().Be("Post.NotFound");
    }

    private async Task<Post> AddPostAsync()
    {
        User user = User.Create("identity-id", "username", "Display Name", null);
        Post post = Post.Create("content", user.Id);
        await _factory.ExecuteDbContextAsync(dbContext =>
        {
            dbContext.AddRange(user, post);
            return dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        return post;
    }

    private sealed record PostBody(Guid Id, Guid AuthorId, string Content, DateTimeOffset CreatedAt, DateTimeOffset ModifiedAt);
}
