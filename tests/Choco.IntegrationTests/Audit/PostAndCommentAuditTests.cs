using System.Net;
using System.Net.Http.Json;

using Choco.Contract.Comments;
using Choco.Contract.Posts;
using Choco.Domain.AuditLogs;
using Choco.Domain.Comments;
using Choco.Domain.Posts;
using Choco.Domain.Users;

namespace Choco.IntegrationTests.Audit;

public sealed class PostAndCommentAuditTests(ChocoApiFactory factory) : AuditTestsBase(factory)
{
    [Fact]
    public async Task CreatePost_RecordsPostCreated()
    {
        User author = await AddUserAsync("identity-id", "username");
        using HttpRequestMessage request = Request(HttpMethod.Post, "/api/v1/posts", new { content = "content" });

        using HttpResponseMessage response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        PostResponse created = (await response.Content.ReadFromJsonAsync<PostResponse>(TestContext.Current.CancellationToken))!;
        AuditLogEntry entry = (await ReadEntriesAsync()).Should().ContainSingle().Subject;
        ShouldBeEntry(entry, author.Id, AuditAction.PostCreated, AuditTargetType.Post, created.Id);
        entry.ActorRoles.Should().Equal("Member");
    }

    [Theory]
    [InlineData("content-updated", true)]
    [InlineData(" content ", false)]
    public async Task EditPost_RecordsOnlyTheChangedField(string content, bool changed)
    {
        User author = await AddUserAsync("identity-id", "username");
        Post post = await AddPostAsync(author);
        using HttpRequestMessage request = Request(HttpMethod.Put, $"/api/v1/posts/{post.Id}", new { content });

        using HttpResponseMessage response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        AuditLogEntry entry = (await ReadEntriesAsync()).Should().ContainSingle().Subject;
        ShouldBeEntry(entry, author.Id, AuditAction.PostEdited, AuditTargetType.Post, post.Id);
        entry.Changes.Should().Equal(changed ? [new AuditChange("content")] : Array.Empty<AuditChange>());
    }

    [Fact]
    public async Task DeletePost_ByAuthor_RecordsPostDeletedWithoutAffectedUser()
    {
        User author = await AddUserAsync("identity-id", "username");
        Post post = await AddPostAsync(author);
        using HttpRequestMessage request = Request(HttpMethod.Delete, $"/api/v1/posts/{post.Id}");

        using HttpResponseMessage response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        AuditLogEntry entry = (await ReadEntriesAsync()).Should().ContainSingle().Subject;
        ShouldBeEntry(entry, author.Id, AuditAction.PostDeleted, AuditTargetType.Post, post.Id);
    }

    [Fact]
    public async Task DeletePost_ByAdministrator_RecordsTheAuthorAsAffectedUserAndTheAdministratorRoles()
    {
        User author = await AddUserAsync("identity-id-2", "username-2");
        User administrator = await AddUserAsync("identity-id", "username", await GetAdministratorRoleIdAsync());
        Post post = await AddPostAsync(author);
        using HttpRequestMessage request = Request(HttpMethod.Delete, $"/api/v1/posts/{post.Id}");

        using HttpResponseMessage response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        AuditLogEntry entry = (await ReadEntriesAsync()).Should().ContainSingle().Subject;
        ShouldBeEntry(entry, administrator.Id, AuditAction.PostDeleted, AuditTargetType.Post, post.Id, author.Id);
        entry.ActorRoles.Should().Equal("Administrator", "Member");
    }

    [Fact]
    public async Task CreateComment_RecordsCommentCreatedWithThePostId()
    {
        User author = await AddUserAsync("identity-id", "username");
        Post post = await AddPostAsync(author);
        using HttpRequestMessage request = Request(HttpMethod.Post, $"/api/v1/posts/{post.Id}/comments", new { content = "content" });

        using HttpResponseMessage response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        CommentResponse created = (await response.Content.ReadFromJsonAsync<CommentResponse>(TestContext.Current.CancellationToken))!;
        AuditLogEntry entry = (await ReadEntriesAsync()).Should().ContainSingle().Subject;
        ShouldBeEntry(entry, author.Id, AuditAction.CommentCreated, AuditTargetType.Comment, created.Id, null, $"PostId: {post.Id}");
    }

    [Fact]
    public async Task ReplyToComment_RecordsCommentCreatedWithThePostIdAndParentId()
    {
        User author = await AddUserAsync("identity-id", "username");
        Post post = await AddPostAsync(author);
        Comment parent = Comment.Create(post.Id, author.Id, "content");
        await Factory.ExecuteDbContextAsync(dbContext =>
        {
            dbContext.Add(parent);
            return dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        using HttpRequestMessage request = Request(HttpMethod.Post, $"/api/v1/comments/{parent.Id}/replies", new { content = "content" });

        using HttpResponseMessage response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        CommentResponse created = (await response.Content.ReadFromJsonAsync<CommentResponse>(TestContext.Current.CancellationToken))!;
        AuditLogEntry entry = (await ReadEntriesAsync()).Should().ContainSingle().Subject;
        ShouldBeEntry(
            entry, author.Id, AuditAction.CommentCreated, AuditTargetType.Comment, created.Id, null,
            $"PostId: {post.Id}, ParentId: {parent.Id}");
    }

    [Fact]
    public async Task DeleteComment_ByAdministrator_RecordsTheAuthorAsAffectedUser()
    {
        User author = await AddUserAsync("identity-id-2", "username-2");
        User administrator = await AddUserAsync("identity-id", "username", await GetAdministratorRoleIdAsync());
        Post post = await AddPostAsync(author);
        Comment comment = Comment.Create(post.Id, author.Id, "content");
        await Factory.ExecuteDbContextAsync(dbContext =>
        {
            dbContext.Add(comment);
            return dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        using HttpRequestMessage request = Request(HttpMethod.Delete, $"/api/v1/comments/{comment.Id}");

        using HttpResponseMessage response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        AuditLogEntry entry = (await ReadEntriesAsync()).Should().ContainSingle().Subject;
        ShouldBeEntry(
            entry, administrator.Id, AuditAction.CommentDeleted, AuditTargetType.Comment, comment.Id, author.Id, $"PostId: {post.Id}");
    }

    [Fact]
    public async Task SetCommentReaction_RecordsCommentReactedWithTheTypeAndTheAuthorAsAffectedUser()
    {
        User author = await AddUserAsync("identity-id-2", "username-2");
        User reactor = await AddUserAsync("identity-id", "username");
        Comment comment = await AddCommentAsync(await AddPostAsync(author), author);
        using HttpRequestMessage request = Request(
            HttpMethod.Put, $"/api/v1/comments/{comment.Id}/reaction", new { type = "dislike" });

        using HttpResponseMessage response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        AuditLogEntry entry = (await ReadEntriesAsync()).Should().ContainSingle().Subject;
        ShouldBeEntry(
            entry, reactor.Id, AuditAction.CommentReacted, AuditTargetType.Comment, comment.Id, author.Id, "Type: dislike");
    }

    [Fact]
    public async Task SetCommentReaction_WhenTheCommentDoesNotExist_RecordsNoEntry()
    {
        await AddUserAsync("identity-id", "username");
        using HttpRequestMessage request = Request(
            HttpMethod.Put, $"/api/v1/comments/{Guid.NewGuid()}/reaction", new { type = "like" });

        using HttpResponseMessage response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ReadEntriesAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task RemoveCommentReaction_RecordsCommentReactionRemovedWithTheAuthorAsAffectedUser()
    {
        User author = await AddUserAsync("identity-id-2", "username-2");
        User reactor = await AddUserAsync("identity-id", "username");
        Comment comment = await AddCommentAsync(await AddPostAsync(author), author);
        using HttpRequestMessage request = Request(HttpMethod.Delete, $"/api/v1/comments/{comment.Id}/reaction");

        using HttpResponseMessage response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        AuditLogEntry entry = (await ReadEntriesAsync()).Should().ContainSingle().Subject;
        ShouldBeEntry(entry, reactor.Id, AuditAction.CommentReactionRemoved, AuditTargetType.Comment, comment.Id, author.Id);
    }

    [Fact]
    public async Task RemoveCommentReaction_WhenTheCommentDoesNotExist_RecordsNoEntry()
    {
        await AddUserAsync("identity-id", "username");
        using HttpRequestMessage request = Request(HttpMethod.Delete, $"/api/v1/comments/{Guid.NewGuid()}/reaction");

        using HttpResponseMessage response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ReadEntriesAsync()).Should().BeEmpty();
    }

    private async Task<Comment> AddCommentAsync(Post post, User author)
    {
        Comment comment = Comment.Create(post.Id, author.Id, "content");
        comment.ClearDomainEvents();
        await Factory.ExecuteDbContextAsync(dbContext =>
        {
            dbContext.Add(comment);
            return dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        return comment;
    }
}
