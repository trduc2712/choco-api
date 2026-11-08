using Choco.Application.Posts;
using Choco.Contract.Abstractions;
using Choco.Contract.Posts;
using Choco.Domain.Posts;
using Choco.Domain.Users;

namespace Choco.Application.UnitTests.Posts;

public sealed class PostPagesTests
{
    [Fact]
    public void ToPage_MapsEachPostWithItsOwnAuthor()
    {
        User firstAuthor = User.Create("identity-id", "username", "Display Name", "https://example.com/avatar.png");
        User secondAuthor = User.Create("identity-id-2", "username-2", "Display Name 2", null);
        Post firstPost = Post.Create("content-1", firstAuthor.Id);
        Post secondPost = Post.Create("content-2", secondAuthor.Id);

        PageResponse<PostResponse> page = PostPages.ToPage([firstPost, secondPost], [secondAuthor, firstAuthor], 10);

        page.Items.Select(item => (item.Id, item.AuthorId, item.Content)).Should().Equal(
            (firstPost.Id, firstAuthor.Id, "content-1"),
            (secondPost.Id, secondAuthor.Id, "content-2"));
        page.Items.Select(item => item.Author).Should().Equal(
            new PostAuthorResponse("username", "Display Name", "https://example.com/avatar.png"),
            new PostAuthorResponse("username-2", "Display Name 2", null));
    }

    [Fact]
    public void ToPage_WithAtMostLimitPosts_HasNoNextCursor()
    {
        User author = User.Create("identity-id", "username", "Display Name", null);
        Post[] posts = [Post.Create("content-1", author.Id), Post.Create("content-2", author.Id)];

        PageResponse<PostResponse> page = PostPages.ToPage(posts, [author], 2);

        page.Items.Should().HaveCount(2);
        page.NextCursor.Should().BeNull();
    }

    [Fact]
    public void ToPage_WithMoreThanLimitPosts_ReturnsLimitPostsAndCursorOfTheLastReturned()
    {
        User author = User.Create("identity-id", "username", "Display Name", null);
        Post[] posts = [Post.Create("content-1", author.Id), Post.Create("content-2", author.Id), Post.Create("content-3", author.Id)];

        PageResponse<PostResponse> page = PostPages.ToPage(posts, [author], 2);

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

        PageResponse<PostResponse> page = PostPages.ToPage([kept, orphan, extra], [author], 2);

        page.Items.Select(item => item.Id).Should().Equal(kept.Id);
        PostCursor.TryDecode(page.NextCursor, out PostCursor? cursor).Should().BeTrue();
        cursor.Should().Be(new PostCursor(orphan.CreatedAt, orphan.Id));
    }

    [Fact]
    public void ToPage_WithoutPosts_ReturnsEmptyPage()
    {
        PageResponse<PostResponse> page = PostPages.ToPage([], [], 2);

        page.Items.Should().BeEmpty();
        page.NextCursor.Should().BeNull();
    }
}
