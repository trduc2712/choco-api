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

    [Fact]
    public async Task EditPost_RecordsPostEdited()
    {
        User author = await AddUserAsync("identity-id", "username");
        Post post = await AddPostAsync(author);
        using HttpRequestMessage request = Request(HttpMethod.Put, $"/api/v1/posts/{post.Id}", new { content = "content-updated" });

        using HttpResponseMessage response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        AuditLogEntry entry = (await ReadEntriesAsync()).Should().ContainSingle().Subject;
        ShouldBeEntry(entry, author.Id, AuditAction.PostEdited, AuditTargetType.Post, post.Id);
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
}
