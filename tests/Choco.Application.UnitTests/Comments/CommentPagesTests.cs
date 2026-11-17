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

        PageResponse<CommentResponse> page = CommentPages.ToPage(
            [Visible(first, 2), Visible(second, 0)], [secondAuthor, firstAuthor], 10);

        page.Items.Select(item => (item.Id, item.PostId, item.ParentId, item.AuthorId, item.Content, item.ReplyCount, item.IsDeleted)).Should().Equal(
            (first.Id, PostId, (Guid?)null, (Guid?)firstAuthor.Id, "content-1", 2, false),
            (second.Id, PostId, (Guid?)null, (Guid?)secondAuthor.Id, "content-2", 0, false));
        page.Items.Select(item => item.Author).Should().Equal(
            new PostAuthorResponse("username", "Display Name", "https://example.com/avatar.png"),
            new PostAuthorResponse("username-2", "Display Name 2", null));
    }

    [Fact]
    public void ToPage_WithReply_MapsItsParent()
    {
        User author = User.Create("identity-id", "username", "Display Name", null);
        Comment parent = Comment.Create(PostId, author.Id, "content-1");
        Comment reply = parent.Reply(author.Id, "content-2").Value;

        PageResponse<CommentResponse> page = CommentPages.ToPage([Visible(reply, 0)], [author], 10);

        page.Items.Should().ContainSingle().Which.ParentId.Should().Be(parent.Id);
    }

    [Fact]
    public void ToPage_WithHiddenComment_MapsItAsPlaceholderWithoutAuthorOrContent()
    {
        Comment hidden = Comment.Create(PostId, new Guid("5f0c6b1e-2d4a-4c8e-9b7a-3e1f2a6d8c40"), "content");

        PageResponse<CommentResponse> page = CommentPages.ToPage([new CommentThreadEntry(hidden, false, 1)], [], 10);

        page.Items.Should().ContainSingle().Which.Should().Be(new CommentResponse(
            hidden.Id, PostId, null, null, null, null, 1, true, hidden.CreatedAt));
    }

    [Fact]
    public void ToPage_WithAtMostLimitComments_HasNoNextCursor()
    {
        User author = User.Create("identity-id", "username", "Display Name", null);
        CommentThreadEntry[] entries =
        [
            Visible(Comment.Create(PostId, author.Id, "content-1"), 0),
            Visible(Comment.Create(PostId, author.Id, "content-2"), 0)
        ];

        PageResponse<CommentResponse> page = CommentPages.ToPage(entries, [author], 2);

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

        PageResponse<CommentResponse> page = CommentPages.ToPage(
            comments.Select(comment => Visible(comment, 0)).ToList(), [author], 2);

        page.Items.Select(item => item.Id).Should().Equal(comments[0].Id, comments[1].Id);
        PostCursor.TryDecode(page.NextCursor, out PostCursor? cursor).Should().BeTrue();
        cursor.Should().Be(new PostCursor(comments[1].CreatedAt, comments[1].Id));
    }

    [Fact]
    public void ToPage_WithVisibleCommentWhoseAuthorIsMissing_LeavesItOutAndKeepsTheCursor()
    {
        User author = User.Create("identity-id", "username", "Display Name", null);
        Comment kept = Comment.Create(PostId, author.Id, "content-1");
        Comment orphan = Comment.Create(PostId, new Guid("6a7b8c9d-0e1f-4a2b-8c3d-4e5f6a7b8c9d"), "content-2");
        Comment extra = Comment.Create(PostId, author.Id, "content-3");

        PageResponse<CommentResponse> page = CommentPages.ToPage(
            [Visible(kept, 0), Visible(orphan, 0), Visible(extra, 0)], [author], 2);

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

    private static CommentThreadEntry Visible(Comment comment, int replyCount)
    {
        return new CommentThreadEntry(comment, true, replyCount);
    }
}
