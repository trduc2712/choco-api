using Choco.Application.Posts;
using Choco.Contract.Abstractions;
using Choco.Contract.Posts;
using Choco.Domain.PostLikes;
using Choco.Domain.Posts;
using Choco.Domain.Users;

namespace Choco.Application.UnitTests.Posts;

public sealed class PostPagesTests
{
    private static readonly IReadOnlyDictionary<Guid, PostLikeSummary> NoLikes = new Dictionary<Guid, PostLikeSummary>();
    private static readonly IReadOnlyDictionary<Guid, int> NoComments = new Dictionary<Guid, int>();

    [Fact]
    public void ToPage_MapsEachPostWithItsOwnAuthor()
    {
        User firstAuthor = User.Create("identity-id", "username", "Display Name", "https://example.com/avatar.png");
        User secondAuthor = User.Create("identity-id-2", "username-2", "Display Name 2", null);
        Post firstPost = Post.Create("content-1", firstAuthor.Id);
        Post secondPost = Post.Create("content-2", secondAuthor.Id);

        PageResponse<PostResponse> page = PostPages.ToPage([firstPost, secondPost], [secondAuthor, firstAuthor], NoLikes, NoComments, 10);

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

        PageResponse<PostResponse> page = PostPages.ToPage([liked, unliked], [author], likes, NoComments, 10);

        page.Items.Select(item => (item.LikeCount, item.IsLikedByMe)).Should().Equal((3, true), (0, false));
    }

    [Fact]
    public void ToPage_MapsEachPostWithItsOwnCommentCount()
    {
        User author = User.Create("identity-id", "username", "Display Name", null);
        Post commented = Post.Create("content-1", author.Id);
        Post uncommented = Post.Create("content-2", author.Id);
        Dictionary<Guid, int> commentCounts = new() { [commented.Id] = 4 };

        PageResponse<PostResponse> page = PostPages.ToPage([commented, uncommented], [author], NoLikes, commentCounts, 10);

        page.Items.Select(item => item.CommentCount).Should().Equal(4, 0);
    }

    [Fact]
    public void ToPage_WithAtMostLimitPosts_HasNoNextCursor()
    {
        User author = User.Create("identity-id", "username", "Display Name", null);
        Post[] posts = [Post.Create("content-1", author.Id), Post.Create("content-2", author.Id)];

        PageResponse<PostResponse> page = PostPages.ToPage(posts, [author], NoLikes, NoComments, 2);

        page.Items.Should().HaveCount(2);
        page.NextCursor.Should().BeNull();
    }

    [Fact]
    public void ToPage_WithMoreThanLimitPosts_ReturnsLimitPostsAndCursorOfTheLastReturned()
    {
        User author = User.Create("identity-id", "username", "Display Name", null);
        Post[] posts = [Post.Create("content-1", author.Id), Post.Create("content-2", author.Id), Post.Create("content-3", author.Id)];

        PageResponse<PostResponse> page = PostPages.ToPage(posts, [author], NoLikes, NoComments, 2);

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

        PageResponse<PostResponse> page = PostPages.ToPage([kept, orphan, extra], [author], NoLikes, NoComments, 2);

        page.Items.Select(item => item.Id).Should().Equal(kept.Id);
        PostCursor.TryDecode(page.NextCursor, out PostCursor? cursor).Should().BeTrue();
        cursor.Should().Be(new PostCursor(orphan.CreatedAt, orphan.Id));
    }

    [Fact]
    public void ToPage_WithoutPosts_ReturnsEmptyPage()
    {
        PageResponse<PostResponse> page = PostPages.ToPage([], [], NoLikes, NoComments, 2);

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
