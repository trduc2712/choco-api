using Choco.Application.Posts;
using Choco.Contract.Abstractions;
using Choco.Contract.Posts;
using Choco.Domain.Comments;
using Choco.Domain.PostLikes;
using Choco.Domain.Posts;
using Choco.Domain.Users;

namespace Choco.Application.UnitTests.Posts;

public sealed class PostPagesTests
{
    private static readonly IReadOnlyDictionary<Guid, PostLikeSummary> NoLikes = new Dictionary<Guid, PostLikeSummary>();
    private static readonly IReadOnlyDictionary<Guid, int> NoComments = new Dictionary<Guid, int>();
    private static readonly IReadOnlyDictionary<Guid, Comment> NoTopComments = new Dictionary<Guid, Comment>();

    [Fact]
    public void ToPage_MapsEachPostWithItsOwnAuthor()
    {
        User firstAuthor = User.Create("identity-id", "username", "Display Name", "https://example.com/avatar.png");
        User secondAuthor = User.Create("identity-id-2", "username-2", "Display Name 2", null);
        Post firstPost = Post.Create("content-1", firstAuthor.Id);
        Post secondPost = Post.Create("content-2", secondAuthor.Id);

        PageResponse<PostResponse> page = PostPages.ToPage([firstPost, secondPost], [secondAuthor, firstAuthor], NoLikes, NoComments, NoTopComments, 10);

        page.Items.Select(item => (item.Id, item.AuthorId, item.Content)).Should().Equal(
            (firstPost.Id, firstAuthor.Id, "content-1"),
            (secondPost.Id, secondAuthor.Id, "content-2"));
        page.Items.Select(item => item.Author).Should().Equal(
            new PostAuthorResponse("username", "Display Name", "https://example.com/avatar.png"),
            new PostAuthorResponse("username-2", "Display Name 2", null));
    }

    [Fact]
    public void ToPage_MapsEachPostWithItsOwnLikes()
    {
        User author = User.Create("identity-id", "username", "Display Name", null);
        Post liked = Post.Create("content-1", author.Id);
        Post unliked = Post.Create("content-2", author.Id);
        Dictionary<Guid, PostLikeSummary> likes = new() { [liked.Id] = new PostLikeSummary(3, true) };

        PageResponse<PostResponse> page = PostPages.ToPage([liked, unliked], [author], likes, NoComments, NoTopComments, 10);

        page.Items.Select(item => (item.LikeCount, item.IsLikedByMe)).Should().Equal((3, true), (0, false));
    }

    [Fact]
    public void ToPage_MapsEachPostWithItsOwnCommentCount()
    {
        User author = User.Create("identity-id", "username", "Display Name", null);
        Post commented = Post.Create("content-1", author.Id);
        Post uncommented = Post.Create("content-2", author.Id);
        Dictionary<Guid, int> commentCounts = new() { [commented.Id] = 4 };

        PageResponse<PostResponse> page = PostPages.ToPage([commented, uncommented], [author], NoLikes, commentCounts, NoTopComments, 10);

        page.Items.Select(item => item.CommentCount).Should().Equal(4, 0);
    }

    [Fact]
    public void ToPage_MapsEachPostWithItsOwnTopComment()
    {
        User author = User.Create("identity-id", "username", "Display Name", null);
        User commenter = User.Create("identity-id-2", "username-2", "Display Name 2", "https://example.com/avatar-2.png");
        Post commented = Post.Create("content-1", author.Id);
        Post uncommented = Post.Create("content-2", author.Id);
        Comment comment = Comment.Create(commented.Id, commenter.Id, "content");
        Dictionary<Guid, Comment> topComments = new() { [commented.Id] = comment };

        PageResponse<PostResponse> page = PostPages.ToPage(
            [commented, uncommented], [author, commenter], NoLikes, NoComments, topComments, 10);

        page.Items.Select(item => item.TopComment).Should().Equal(
            new PostTopCommentResponse(
                comment.Id, new PostAuthorResponse("username-2", "Display Name 2", "https://example.com/avatar-2.png"), "content"),
            null);
    }

    [Fact]
    public void ToPage_WithTopCommentWhoseAuthorIsMissing_ShowsNoTopComment()
    {
        User author = User.Create("identity-id", "username", "Display Name", null);
        Post post = Post.Create("content", author.Id);
        Guid missingAuthorId = new("4d5e6f7a-8b9c-4d0e-9f1a-2b3c4d5e6f7a");
        Dictionary<Guid, Comment> topComments = new() { [post.Id] = Comment.Create(post.Id, missingAuthorId, "content") };

        PageResponse<PostResponse> page = PostPages.ToPage([post], [author], NoLikes, NoComments, topComments, 10);

        page.Items.Should().ContainSingle().Which.TopComment.Should().BeNull();
    }

    [Fact]
    public void ToPage_WithAtMostLimitPosts_HasNoNextCursor()
    {
        User author = User.Create("identity-id", "username", "Display Name", null);
        Post[] posts = [Post.Create("content-1", author.Id), Post.Create("content-2", author.Id)];

        PageResponse<PostResponse> page = PostPages.ToPage(posts, [author], NoLikes, NoComments, NoTopComments, 2);

        page.Items.Should().HaveCount(2);
        page.NextCursor.Should().BeNull();
    }

    [Fact]
    public void ToPage_WithMoreThanLimitPosts_ReturnsLimitPostsAndCursorOfTheLastReturned()
    {
        User author = User.Create("identity-id", "username", "Display Name", null);
        Post[] posts = [Post.Create("content-1", author.Id), Post.Create("content-2", author.Id), Post.Create("content-3", author.Id)];

        PageResponse<PostResponse> page = PostPages.ToPage(posts, [author], NoLikes, NoComments, NoTopComments, 2);

        page.Items.Select(item => item.Id).Should().Equal(posts[0].Id, posts[1].Id);
        PostCursor.TryDecode(page.NextCursor, out PostCursor? cursor).Should().BeTrue();
        cursor.Should().Be(new PostCursor(posts[1].CreatedAt, posts[1].Id));
    }

    [Fact]
    public void ToPage_WithPostWhoseAuthorIsMissing_LeavesItOutAndKeepsTheCursor()
    {
        User author = User.Create("identity-id", "username", "Display Name", null);
        Post kept = Post.Create("content-1", author.Id);
        Post orphan = Post.Create("content-2", Guid.NewGuid());
        Post extra = Post.Create("content-3", author.Id);

        PageResponse<PostResponse> page = PostPages.ToPage([kept, orphan, extra], [author], NoLikes, NoComments, NoTopComments, 2);

        page.Items.Select(item => item.Id).Should().Equal(kept.Id);
        PostCursor.TryDecode(page.NextCursor, out PostCursor? cursor).Should().BeTrue();
        cursor.Should().Be(new PostCursor(orphan.CreatedAt, orphan.Id));
    }

    [Fact]
    public void ToPage_WithoutPosts_ReturnsEmptyPage()
    {
        PageResponse<PostResponse> page = PostPages.ToPage([], [], NoLikes, NoComments, NoTopComments, 2);

        page.Items.Should().BeEmpty();
        page.NextCursor.Should().BeNull();
    }

    [Fact]
    public void ReadPosition_WithoutCursor_ReturnsNull()
    {
        (DateTimeOffset CreatedAt, Guid Id)? position = PostPages.ReadPosition(null);

        position.Should().BeNull();
    }

    [Fact]
    public void ReadPosition_WithValidCursor_ReturnsItsPosition()
    {
        DateTimeOffset createdAt = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        Guid id = new("3c4d5e6f-7a8b-4c9d-8e0f-1a2b3c4d5e6f");

        (DateTimeOffset CreatedAt, Guid Id)? position = PostPages.ReadPosition(new PostCursor(createdAt, id).Encode());

        position.Should().Be((createdAt, id));
    }
}
