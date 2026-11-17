using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using Choco.Domain.CommentReactions;
using Choco.Domain.Comments;
using Choco.Domain.Posts;
using Choco.Domain.Users;
using Choco.IntegrationTests.Authentication;
using Choco.IntegrationTests.Users;

namespace Choco.IntegrationTests.Comments;

public sealed class CommentReactionFieldsTests : IClassFixture<ChocoApiFactory>, IAsyncLifetime
{
    private readonly ChocoApiFactory _factory;
    private readonly HttpClient _client;

    public CommentReactionFieldsTests(ChocoApiFactory factory)
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
    public async Task PostComments_ShowEachCommentsCountsAndTheCallersReaction()
    {
        User caller = await AddUserAsync("identity-id", "username");
        User second = await AddUserAsync("identity-id-2", "username-2");
        User third = await AddUserAsync("identity-id-3", "username-3");
        Post post = await AddPostAsync(second);
        Comment reacted = await AddCommentAsync(Comment.Create(post.Id, second.Id, "content-1"));
        Comment disliked = await AddCommentAsync(Comment.Create(post.Id, second.Id, "content-2"));
        Comment unreacted = await AddCommentAsync(Comment.Create(post.Id, second.Id, "content-3"));
        await CommentReactionTestData.AddReactionsAsync(
            _factory,
            (reacted, caller, CommentReactionType.Like),
            (reacted, second, CommentReactionType.Like),
            (reacted, third, CommentReactionType.Dislike),
            (disliked, caller, CommentReactionType.Dislike));
        using HttpRequestMessage request = Request($"/api/v1/posts/{post.Id}/comments");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement[] items = await ReadItemsAsync(response);
        items.Select(Reactions).Should().Equal(
            (reacted.Id, 2, 1, "like"),
            (disliked.Id, 0, 1, "dislike"),
            (unreacted.Id, 0, 0, (string?)null));
    }

    [Fact]
    public async Task Counts_LeaveOutDeletedAccountsAndUsersBlockedEitherWay()
    {
        User caller = await AddUserAsync("identity-id", "username");
        User author = await AddUserAsync("identity-id-2", "username-2");
        User deleted = await AddUserAsync("identity-id-3", "username-3");
        User blockedByCaller = await AddUserAsync("identity-id-4", "username-4");
        User blockerOfCaller = await AddUserAsync("identity-id-5", "username-5");
        Post post = await AddPostAsync(author);
        Comment comment = await AddCommentAsync(Comment.Create(post.Id, author.Id, "content"));
        await CommentReactionTestData.AddReactionsAsync(
            _factory,
            (comment, author, CommentReactionType.Like),
            (comment, deleted, CommentReactionType.Like),
            (comment, blockedByCaller, CommentReactionType.Dislike),
            (comment, blockerOfCaller, CommentReactionType.Dislike));
        await DeleteUserAsync(deleted);
        await UserBlockTestData.AddBlockAsync(_factory, caller, blockedByCaller);
        await UserBlockTestData.AddBlockAsync(_factory, blockerOfCaller, caller);
        using HttpRequestMessage request = Request($"/api/v1/posts/{post.Id}/comments");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement item = (await ReadItemsAsync(response)).Should().ContainSingle().Subject;
        Reactions(item).Should().Be((comment.Id, 1, 0, (string?)null));
    }

    [Fact]
    public async Task Replies_ShowTheirCountsAndTheCallersReaction()
    {
        User caller = await AddUserAsync("identity-id", "username");
        User author = await AddUserAsync("identity-id-2", "username-2");
        Post post = await AddPostAsync(author);
        Comment parent = Comment.Create(post.Id, author.Id, "content-1");
        Comment reply = parent.Reply(author.Id, "content-2").Value;
        await AddCommentAsync(parent, reply);
        await CommentReactionTestData.AddReactionsAsync(
            _factory,
            (parent, author, CommentReactionType.Like),
            (reply, caller, CommentReactionType.Dislike),
            (reply, author, CommentReactionType.Like));
        using HttpRequestMessage request = Request($"/api/v1/comments/{parent.Id}/replies");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement item = (await ReadItemsAsync(response)).Should().ContainSingle().Subject;
        Reactions(item).Should().Be((reply.Id, 1, 1, "dislike"));
    }

    [Fact]
    public async Task Placeholder_HasNoReactions()
    {
        User caller = await AddUserAsync("identity-id", "username");
        User blocked = await AddUserAsync("identity-id-2", "username-2");
        Post post = await AddPostAsync(caller);
        Comment hidden = Comment.Create(post.Id, blocked.Id, "content-1");
        Comment reply = hidden.Reply(caller.Id, "content-2").Value;
        await AddCommentAsync(hidden, reply);
        await CommentReactionTestData.AddReactionsAsync(_factory, (hidden, caller, CommentReactionType.Like));
        await UserBlockTestData.AddBlockAsync(_factory, caller, blocked);
        using HttpRequestMessage request = Request($"/api/v1/posts/{post.Id}/comments");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement item = (await ReadItemsAsync(response)).Should().ContainSingle().Subject;
        item.GetProperty("isDeleted").GetBoolean().Should().BeTrue();
        Reactions(item).Should().Be((hidden.Id, 0, 0, (string?)null));
    }

    private static (Guid Id, int LikeCount, int DislikeCount, string? MyReaction) Reactions(JsonElement item)
    {
        return (
            item.GetProperty("id").GetGuid(),
            item.GetProperty("likeCount").GetInt32(),
            item.GetProperty("dislikeCount").GetInt32(),
            item.GetProperty("myReaction").GetString());
    }

    private static async Task<JsonElement[]> ReadItemsAsync(HttpResponseMessage response)
    {
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        return body.GetProperty("items").EnumerateArray().ToArray();
    }

    private static HttpRequestMessage Request(string path)
    {
        HttpRequestMessage request = new(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TestJwt.Create(DateTimeOffset.UtcNow));
        return request;
    }

    private async Task<User> AddUserAsync(string identityId, string username)
    {
        User user = User.Create(identityId, username, "Display Name", null);
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

    private async Task<Comment> AddCommentAsync(params Comment[] comments)
    {
        foreach (Comment comment in comments)
        {
            comment.ClearDomainEvents();
            _factory.TimeProvider.Advance(TimeSpan.FromSeconds(1));
            await _factory.ExecuteDbContextAsync(db =>
            {
                db.Add(comment);
                return db.SaveChangesAsync(TestContext.Current.CancellationToken);
            });
        }

        return comments[^1];
    }

    private Task<int> DeleteUserAsync(User user)
    {
        return _factory.ExecuteDbContextAsync(db =>
        {
            db.Remove(db.Set<User>().Single(candidate => candidate.Id == user.Id));
            return db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
    }
}
