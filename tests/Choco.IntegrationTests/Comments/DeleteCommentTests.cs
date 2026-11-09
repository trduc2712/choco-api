using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using Choco.Domain.Comments;
using Choco.Domain.Posts;
using Choco.Domain.Roles;
using Choco.Domain.Users;
using Choco.IntegrationTests.Authentication;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Choco.IntegrationTests.Comments;

public sealed class DeleteCommentTests : IClassFixture<ChocoApiFactory>, IAsyncLifetime
{
    private static readonly Guid UnknownCommentId = new("00000000-0000-0000-0000-000000000001");
    private readonly ChocoApiFactory _factory;
    private readonly HttpClient _client;

    public DeleteCommentTests(ChocoApiFactory factory)
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
        using HttpRequestMessage request = new(HttpMethod.Delete, $"/api/v1/comments/{UnknownCommentId}");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Author_DeletesTheirCommentOnly()
    {
        User caller = await AddUserAsync("identity-id", "username");
        Post post = await AddPostAsync(await AddUserAsync("identity-id-2", "username-2"));
        Comment own = await AddCommentAsync(post, caller);
        Comment other = await AddCommentAsync(post, caller);
        using HttpRequestMessage request = Request(own.Id);

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        Comment deleted = (await GetCommentAsync(own.Id));
        deleted.IsDeleted.Should().BeTrue();
        deleted.DeletedAt.Should().Be(_factory.TimeProvider.GetUtcNow());
        (await GetCommentAsync(other.Id)).IsDeleted.Should().BeFalse();
        _factory.IdentityProvider.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task UserWithTheDeleteAnyPermission_DeletesAnotherUsersComment()
    {
        User author = await AddUserAsync("identity-id-2", "username-2");
        Guid adminRoleId = await _factory.ExecuteDbContextAsync(db => db.Set<Role>()
            .Where(role => role.SystemKey == SystemRoles.Admin)
            .Select(role => role.Id)
            .SingleAsync(TestContext.Current.CancellationToken));
        await AddUserAsync("identity-id", "username", adminRoleId);
        Post post = await AddPostAsync(author);
        Comment comment = await AddCommentAsync(post, author);
        using HttpRequestMessage request = Request(comment.Id);

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await GetCommentAsync(comment.Id)).IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task AnotherUserWithoutThePermission_ReturnsForbiddenAndKeepsTheComment()
    {
        await AddUserAsync("identity-id", "username");
        User author = await AddUserAsync("identity-id-2", "username-2");
        Post post = await AddPostAsync(author);
        Comment comment = await AddCommentAsync(post, author);
        using HttpRequestMessage request = Request(comment.Id);

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await AssertProblemAsync(response, HttpStatusCode.Forbidden, "Comment.NotAuthor");
        (await GetCommentAsync(comment.Id)).IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task DeletingTwice_ReturnsNotFoundTheSecondTime()
    {
        User caller = await AddUserAsync("identity-id", "username");
        Post post = await AddPostAsync(caller);
        Comment comment = await AddCommentAsync(post, caller);
        using HttpRequestMessage first = Request(comment.Id);
        using HttpRequestMessage second = Request(comment.Id);

        using HttpResponseMessage firstResponse = await _client.SendAsync(first, TestContext.Current.CancellationToken);
        using HttpResponseMessage secondResponse = await _client.SendAsync(second, TestContext.Current.CancellationToken);

        firstResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await AssertProblemAsync(secondResponse, HttpStatusCode.NotFound, "Comment.NotFound");
    }

    [Fact]
    public async Task UnknownComment_ReturnsNotFound()
    {
        await AddUserAsync("identity-id", "username");
        using HttpRequestMessage request = Request(UnknownCommentId);

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "Comment.NotFound");
    }

    [Fact]
    public async Task CommentOnADeletedPost_ReturnsNotFoundAndKeepsTheComment()
    {
        User caller = await AddUserAsync("identity-id", "username");
        Post post = await AddPostAsync(caller);
        Comment comment = await AddCommentAsync(post, caller);
        await _factory.ExecuteDbContextAsync(db =>
        {
            db.Remove(db.Set<Post>().Single(candidate => candidate.Id == post.Id));
            return db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        using HttpRequestMessage request = Request(comment.Id);

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "Comment.NotFound");
        (await GetCommentAsync(comment.Id)).IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task CommentOnAPostOfADeletedAuthor_ReturnsNotFound()
    {
        User caller = await AddUserAsync("identity-id", "username");
        User author = await AddUserAsync("identity-id-2", "username-2");
        Post post = await AddPostAsync(author);
        Comment comment = await AddCommentAsync(post, caller);
        await _factory.ExecuteDbContextAsync(db =>
        {
            db.Remove(db.Set<User>().Single(user => user.Id == author.Id));
            return db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        using HttpRequestMessage request = Request(comment.Id);

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "Comment.NotFound");
    }

    [Fact]
    public async Task EmptyCommentId_ReturnsValidationProblem()
    {
        await AddUserAsync("identity-id", "username");
        using HttpRequestMessage request = Request(Guid.Empty);

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        ValidationProblemDetails problem = (await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(TestContext.Current.CancellationToken))!;
        problem.Errors.Keys.Should().ContainSingle().Which.Should().Be("CommentId");
        problem.Extensions["code"].Should().BeOfType<JsonElement>().Which.GetString().Should().Be("Validation.Failed");
    }

    [Fact]
    public async Task DeletedCaller_ReturnsUnauthorized()
    {
        User caller = await AddUserAsync("identity-id", "username");
        await _factory.ExecuteDbContextAsync(db =>
        {
            db.Remove(db.Set<User>().Single(user => user.Id == caller.Id));
            return db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        using HttpRequestMessage request = Request(UnknownCommentId);

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "User.Deleted");
    }

    private static HttpRequestMessage Request(Guid commentId)
    {
        HttpRequestMessage request = new(HttpMethod.Delete, $"/api/v1/comments/{commentId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TestJwt.Create(DateTimeOffset.UtcNow));
        return request;
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        response.StatusCode.Should().Be(status);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        ProblemDetails problem = (await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken))!;
        problem.Status.Should().Be((int)status);
        problem.Extensions["code"].Should().BeOfType<JsonElement>().Which.GetString().Should().Be(code);
    }

    private async Task<User> AddUserAsync(string identityId, string username, Guid? roleId = null)
    {
        User user = User.Create(identityId, username, "Display Name", null);

        if (roleId is not null)
        {
            user.AssignRole(roleId.Value, null, ChocoApiFactory.StartTime);
        }

        await _factory.ExecuteDbContextAsync(db =>
        {
            db.Add(user);
            return db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        return user;
    }

    private async Task<Post> AddPostAsync(User author)
    {
        Post post = Post.Create("content", author.Id);
        await _factory.ExecuteDbContextAsync(db =>
        {
            db.Add(post);
            return db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        return post;
    }

    private async Task<Comment> AddCommentAsync(Post post, User author)
    {
        Comment comment = Comment.Create(post.Id, author.Id, "content");
        await _factory.ExecuteDbContextAsync(db =>
        {
            db.Add(comment);
            return db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        return comment;
    }

    private Task<Comment> GetCommentAsync(Guid id)
    {
        return _factory.ExecuteDbContextAsync(db => db.Set<Comment>().IgnoreQueryFilters().AsNoTracking()
            .SingleAsync(comment => comment.Id == id, TestContext.Current.CancellationToken));
    }
}
