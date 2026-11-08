using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using Choco.Domain.Posts;
using Choco.Domain.Users;
using Choco.IntegrationTests.Authentication;

using Microsoft.AspNetCore.Mvc;

namespace Choco.IntegrationTests.Users;

public sealed class GetUserPostsTests(ChocoApiFactory factory) : IClassFixture<ChocoApiFactory>, IAsyncLifetime
{
    private const string MissingUserId = "00000000-0000-0000-0000-000000000001";
    private readonly HttpClient _client = factory.CreateClient();

    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task NoToken_ReturnsUnauthorized()
    {
        using HttpResponseMessage response = await _client.GetAsync(
            $"/api/v1/users/{MissingUserId}/posts", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeletedCaller_ReturnsUnauthorized()
    {
        (User caller, User author) = await AddUsersAsync();
        await factory.ExecuteDbContextAsync(db =>
        {
            db.Remove(db.Set<User>().Single(user => user.Id == caller.Id));
            return db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        using HttpResponseMessage response = await GetAsync($"/api/v1/users/{author.Id}/posts");

        await ShouldBeProblemAsync(response, HttpStatusCode.Unauthorized, "User.Deleted");
    }

    [Fact]
    public async Task EmptyUserId_ReturnsValidationProblem()
    {
        await AddUsersAsync();

        using HttpResponseMessage response = await GetAsync($"/api/v1/users/{Guid.Empty}/posts");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        ValidationProblemDetails body = (await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(TestContext.Current.CancellationToken))!;
        body.Status.Should().Be(400);
        body.Errors.Keys.Should().ContainSingle().Which.Should().Be("UserId");
        body.Extensions["code"].Should().BeOfType<JsonElement>().Which.GetString().Should().Be("Validation.Failed");
    }

    [Fact]
    public async Task UnknownAuthor_ReturnsNotFound()
    {
        await AddUsersAsync();

        using HttpResponseMessage response = await GetAsync($"/api/v1/users/{MissingUserId}/posts");

        await ShouldBeProblemAsync(response, HttpStatusCode.NotFound, "User.NotFound");
    }

    [Fact]
    public async Task DeletedAuthor_ReturnsNotFound()
    {
        (_, User author) = await AddUsersAsync();
        await AddPostAsync(author, "content");
        await factory.ExecuteDbContextAsync(db =>
        {
            db.Remove(db.Set<User>().Single(user => user.Id == author.Id));
            return db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        using HttpResponseMessage response = await GetAsync($"/api/v1/users/{author.Id}/posts");

        await ShouldBeProblemAsync(response, HttpStatusCode.NotFound, "User.NotFound");
    }

    [Fact]
    public async Task AuthorWithoutPosts_ReturnsEmptyPage()
    {
        (_, User author) = await AddUsersAsync();

        using HttpResponseMessage response = await GetAsync($"/api/v1/users/{author.Id}/posts");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        PageBody body = (await response.Content.ReadFromJsonAsync<PageBody>(TestContext.Current.CancellationToken))!;
        body.Items.Should().BeEmpty();
        body.NextCursor.Should().BeNull();
    }

    [Fact]
    public async Task OtherUser_ReturnsOnlyTheirActivePostsNewestFirstAcrossPages()
    {
        (User caller, User author) = await AddUsersAsync();
        Post older = await AddPostAsync(author, "content-1");
        Post newer = await AddPostAsync(author, "content-2");
        Post deleted = await AddPostAsync(author, "content-3");
        await AddPostAsync(caller, "content-4");
        await factory.ExecuteDbContextAsync(db =>
        {
            db.Remove(db.Set<Post>().Single(post => post.Id == deleted.Id));
            return db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        string path = $"/api/v1/users/{author.Id}/posts?limit=1";

        using HttpResponseMessage firstResponse = await GetAsync(path);

        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        PageBody firstPage = (await firstResponse.Content.ReadFromJsonAsync<PageBody>(TestContext.Current.CancellationToken))!;
        firstPage.Items.Should().Equal(new PostBody(
            newer.Id, author.Id, new AuthorBody("username-2", "Display Name 2", null), newer.Content, newer.CreatedAt, newer.ModifiedAt));
        firstPage.NextCursor.Should().NotBeNullOrEmpty();

        using HttpResponseMessage secondResponse = await GetAsync($"{path}&cursor={firstPage.NextCursor}");

        secondResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        PageBody secondPage = (await secondResponse.Content.ReadFromJsonAsync<PageBody>(TestContext.Current.CancellationToken))!;
        secondPage.Items.Should().Equal(new PostBody(
            older.Id, author.Id, new AuthorBody("username-2", "Display Name 2", null), older.Content, older.CreatedAt, older.ModifiedAt));
        secondPage.NextCursor.Should().BeNull();
    }

    private async Task<HttpResponseMessage> GetAsync(string path)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TestJwt.Create(DateTimeOffset.UtcNow));
        return await _client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private async Task<(User Caller, User Author)> AddUsersAsync()
    {
        User caller = User.Create("identity-id", "username", "Display Name", null);
        User author = User.Create("identity-id-2", "username-2", "Display Name 2", null);
        await factory.ExecuteDbContextAsync(db =>
        {
            db.AddRange(caller, author);
            return db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        return (caller, author);
    }

    private async Task<Post> AddPostAsync(User author, string content)
    {
        factory.TimeProvider.Advance(TimeSpan.FromSeconds(1));
        Post post = Post.Create(content, author.Id);
        await factory.ExecuteDbContextAsync(db =>
        {
            db.Add(post);
            return db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        return post;
    }

    private static async Task ShouldBeProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        response.StatusCode.Should().Be(status);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        ProblemDetails body = (await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken))!;
        body.Status.Should().Be((int)status);
        body.Extensions["code"].Should().BeOfType<JsonElement>().Which.GetString().Should().Be(code);
    }

    private sealed record PageBody(List<PostBody> Items, string? NextCursor);

    private sealed record AuthorBody(string Username, string DisplayName, string? AvatarUrl);

    private sealed record PostBody(Guid Id, Guid AuthorId, AuthorBody Author, string Content, DateTimeOffset CreatedAt, DateTimeOffset ModifiedAt);
}
