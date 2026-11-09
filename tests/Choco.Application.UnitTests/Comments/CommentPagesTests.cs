using Choco.Application.Comments;
using Choco.Application.Posts;
using Choco.Contract.Abstractions;
using Choco.Contract.Comments;
using Choco.Contract.Posts;
using Choco.Domain.Comments;
using Choco.Domain.Users;

namespace Choco.Application.UnitTests.Comments;

public sealed class CommentPagesTests
{
    private static readonly Guid PostId = new("00000000-0000-0000-0000-000000000001");

    [Fact]
    public void ToPage_MapsEachCommentWithItsOwnAuthor()
    {
        User firstAuthor = User.Create("identity-id", "username", "Display Name", "https://example.com/avatar.png");
        User secondAuthor = User.Create("identity-id-2", "username-2", "Display Name 2", null);
        Comment first = Comment.Create(PostId, firstAuthor.Id, "content-1");
        Comment second = Comment.Create(PostId, secondAuthor.Id, "content-2");

        PageResponse<CommentResponse> page = CommentPages.ToPage([first, second], [secondAuthor, firstAuthor], 10);

        page.Items.Select(item => (item.Id, item.PostId, item.AuthorId, item.Content)).Should().Equal(
            (first.Id, PostId, firstAuthor.Id, "content-1"),
            (second.Id, PostId, secondAuthor.Id, "content-2"));
        page.Items.Select(item => item.Author).Should().Equal(
            new PostAuthorResponse("username", "Display Name", "https://example.com/avatar.png"),
            new PostAuthorResponse("username-2", "Display Name 2", null));
    }

    [Fact]
    public void ToPage_WithAtMostLimitComments_HasNoNextCursor()
    {
        User author = User.Create("identity-id", "username", "Display Name", null);
        Comment[] comments = [Comment.Create(PostId, author.Id, "content-1"), Comment.Create(PostId, author.Id, "content-2")];

        PageResponse<CommentResponse> page = CommentPages.ToPage(comments, [author], 2);

        page.Items.Should().HaveCount(2);
        page.NextCursor.Should().BeNull();
    }

    [Fact]
    public void ToPage_WithMoreThanLimitComments_ReturnsLimitCommentsAndCursorOfTheLastReturned()
    {
        User author = User.Create("identity-id", "username", "Display Name", null);
        Comment[] comments =
        [
            Comment.Create(PostId, author.Id, "content-1"),
            Comment.Create(PostId, author.Id, "content-2"),
            Comment.Create(PostId, author.Id, "content-3")
        ];

        PageResponse<CommentResponse> page = CommentPages.ToPage(comments, [author], 2);

        page.Items.Select(item => item.Id).Should().Equal(comments[0].Id, comments[1].Id);
        PostCursor.TryDecode(page.NextCursor, out PostCursor? cursor).Should().BeTrue();
        cursor.Should().Be(new PostCursor(comments[1].CreatedAt, comments[1].Id));
    }

    [Fact]
    public void ToPage_WithCommentWhoseAuthorIsMissing_LeavesItOutAndKeepsTheCursor()
    {
        User author = User.Create("identity-id", "username", "Display Name", null);
        Comment kept = Comment.Create(PostId, author.Id, "content-1");
        Comment orphan = Comment.Create(PostId, Guid.NewGuid(), "content-2");
        Comment extra = Comment.Create(PostId, author.Id, "content-3");

        PageResponse<CommentResponse> page = CommentPages.ToPage([kept, orphan, extra], [author], 2);

        page.Items.Select(item => item.Id).Should().Equal(kept.Id);
        PostCursor.TryDecode(page.NextCursor, out PostCursor? cursor).Should().BeTrue();
        cursor.Should().Be(new PostCursor(orphan.CreatedAt, orphan.Id));
    }

    [Fact]
    public void ToPage_WithoutComments_ReturnsEmptyPage()
    {
        PageResponse<CommentResponse> page = CommentPages.ToPage([], [], 2);

        page.Items.Should().BeEmpty();
        page.NextCursor.Should().BeNull();
    }
}
