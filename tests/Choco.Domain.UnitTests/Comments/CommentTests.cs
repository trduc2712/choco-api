using Choco.Domain.Comments;
using Choco.SharedKernel.Results;

namespace Choco.Domain.UnitTests.Comments;

public sealed class CommentTests
{
    private static readonly Guid PostId = new("00000000-0000-0000-0000-000000000001");
    private static readonly Guid AuthorId = new("5f0c6b1e-2d4a-4c8e-9b7a-3e1f2a6d8c40");
    private static readonly Guid OtherUserId = new("6a7b8c9d-0e1f-4a2b-8c3d-4e5f6a7b8c9d");

    [Fact]
    public void Create_WithValidData_ReturnsCommentWithThatData()
    {
        Comment comment = Comment.Create(PostId, AuthorId, "content");

        comment.Id.Should().NotBeEmpty();
        comment.PostId.Should().Be(PostId);
        comment.AuthorId.Should().Be(AuthorId);
        comment.Content.Should().Be("content");
        comment.ParentId.Should().BeNull();
        comment.RootId.Should().BeNull();
        comment.Depth.Should().Be(0);
        comment.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public void Create_WithValidData_RaisesCommentCreated()
    {
        Comment comment = Comment.Create(PostId, AuthorId, "content");

        comment.DomainEvents.Should().ContainSingle()
            .Which.Should().Be(new CommentCreated(comment.Id, PostId, AuthorId, null));
    }

    [Fact]
    public void Create_WithSurroundingWhitespace_StoresContentTrimmedAtBothEnds()
    {
        Comment comment = Comment.Create(PostId, AuthorId, " \n\tcontent-line-1\n\n  content-line-2 \t\n");

        comment.Content.Should().Be("content-line-1\n\n  content-line-2");
    }

    [Fact]
    public void Create_WithContentAtMaxLength_ReturnsCommentWithThatContent()
    {
        string content = new('c', Comment.ContentMaxLength);

        Comment comment = Comment.Create(PostId, AuthorId, content);

        comment.Content.Should().Be(content);
    }

    [Fact]
    public void Create_WithContentAtMaxLengthAfterTrimming_ReturnsCommentWithTrimmedContent()
    {
        string trimmedContent = new('c', Comment.ContentMaxLength);

        Comment comment = Comment.Create(PostId, AuthorId, $"  {trimmedContent}  ");

        comment.Content.Should().Be(trimmedContent);
    }

    [Fact]
    public void Create_WithContentOverMaxLength_ThrowsArgumentException()
    {
        string content = new('c', Comment.ContentMaxLength + 1);

        Func<Comment> act = () => Comment.Create(PostId, AuthorId, content);

        act.Should().Throw<ArgumentException>().WithParameterName("content");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Create_WithBlankContent_ThrowsArgumentException(string? content)
    {
        Func<Comment> act = () => Comment.Create(PostId, AuthorId, content!);

        act.Should().Throw<ArgumentException>().WithParameterName("content");
    }

    [Fact]
    public void Create_WithEmptyPostId_ThrowsArgumentException()
    {
        Func<Comment> act = () => Comment.Create(Guid.Empty, AuthorId, "content");

        act.Should().Throw<ArgumentException>().WithParameterName("postId");
    }

    [Fact]
    public void Create_WithEmptyAuthorId_ThrowsArgumentException()
    {
        Func<Comment> act = () => Comment.Create(PostId, Guid.Empty, "content");

        act.Should().Throw<ArgumentException>().WithParameterName("authorId");
    }

    [Fact]
    public void Reply_ToTopLevelComment_ReturnsReplyOneLevelDeeperOnTheSamePost()
    {
        Comment parent = Comment.Create(PostId, AuthorId, "content");

        Result<Comment> result = parent.Reply(OtherUserId, "reply-content");

        result.IsSuccess.Should().BeTrue();
        Comment reply = result.Value;
        reply.Id.Should().NotBeEmpty().And.NotBe(parent.Id);
        reply.PostId.Should().Be(PostId);
        reply.AuthorId.Should().Be(OtherUserId);
        reply.Content.Should().Be("reply-content");
        reply.ParentId.Should().Be(parent.Id);
        reply.RootId.Should().Be(parent.Id);
        reply.Depth.Should().Be(1);
        reply.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public void Reply_ToReply_KeepsTheTopLevelCommentAsRoot()
    {
        Comment root = Comment.Create(PostId, AuthorId, "content");
        Comment parent = root.Reply(OtherUserId, "reply-content-1").Value;

        Result<Comment> result = parent.Reply(AuthorId, "reply-content-2");

        result.IsSuccess.Should().BeTrue();
        result.Value.ParentId.Should().Be(parent.Id);
        result.Value.RootId.Should().Be(root.Id);
        result.Value.Depth.Should().Be(2);
    }

    [Fact]
    public void Reply_WithValidData_RaisesCommentCreatedWithParentOnTheReply()
    {
        Comment parent = Comment.Create(PostId, AuthorId, "content");
        parent.ClearDomainEvents();

        Comment reply = parent.Reply(OtherUserId, "reply-content").Value;

        reply.DomainEvents.Should().ContainSingle()
            .Which.Should().Be(new CommentCreated(reply.Id, PostId, OtherUserId, parent.Id));
        parent.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Reply_WithSurroundingWhitespace_StoresContentTrimmedAtBothEnds()
    {
        Comment parent = Comment.Create(PostId, AuthorId, "content");

        Comment reply = parent.Reply(OtherUserId, " \n reply-content \t").Value;

        reply.Content.Should().Be("reply-content");
    }

    [Fact]
    public void Reply_ToCommentOneLevelAboveMaxDepth_ReturnsReplyAtMaxDepth()
    {
        Comment parent = CommentAtDepth(Comment.MaxDepth - 1);

        Result<Comment> result = parent.Reply(OtherUserId, "reply-content");

        result.IsSuccess.Should().BeTrue();
        result.Value.Depth.Should().Be(Comment.MaxDepth);
    }

    [Fact]
    public void Reply_ToCommentAtMaxDepth_ReturnsMaxDepthReached()
    {
        Comment parent = CommentAtDepth(Comment.MaxDepth);
        parent.ClearDomainEvents();

        Result<Comment> result = parent.Reply(OtherUserId, "reply-content");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(CommentErrors.MaxDepthReached);
        parent.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Reply_WithContentAtMaxLength_ReturnsReplyWithThatContent()
    {
        Comment parent = Comment.Create(PostId, AuthorId, "content");
        string content = new('c', Comment.ContentMaxLength);

        Comment reply = parent.Reply(OtherUserId, content).Value;

        reply.Content.Should().Be(content);
    }

    [Fact]
    public void Reply_WithContentOverMaxLength_ThrowsArgumentException()
    {
        Comment parent = Comment.Create(PostId, AuthorId, "content");
        string content = new('c', Comment.ContentMaxLength + 1);

        Func<Result<Comment>> act = () => parent.Reply(OtherUserId, content);

        act.Should().Throw<ArgumentException>().WithParameterName("content");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Reply_WithBlankContent_ThrowsArgumentException(string? content)
    {
        Comment parent = Comment.Create(PostId, AuthorId, "content");

        Func<Result<Comment>> act = () => parent.Reply(OtherUserId, content!);

        act.Should().Throw<ArgumentException>().WithParameterName("content");
    }

    [Fact]
    public void Reply_WithEmptyAuthorId_ThrowsArgumentException()
    {
        Comment parent = Comment.Create(PostId, AuthorId, "content");

        Func<Result<Comment>> act = () => parent.Reply(Guid.Empty, "reply-content");

        act.Should().Throw<ArgumentException>().WithParameterName("authorId");
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void CheckCanDelete_AuthorOrPermittedUser_ReturnsSuccess(bool isAuthor, bool canDeleteAnyComment)
    {
        Comment comment = Comment.Create(PostId, AuthorId, "content");
        Guid userId = isAuthor ? AuthorId : OtherUserId;

        Result result = comment.CheckCanDelete(userId, canDeleteAnyComment);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void CheckCanDelete_AnotherUserWithoutPermission_ReturnsNotAuthor()
    {
        Comment comment = Comment.Create(PostId, AuthorId, "content");

        Result result = comment.CheckCanDelete(OtherUserId, canDeleteAnyComment: false);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(CommentErrors.NotAuthor);
    }

    private static Comment CommentAtDepth(int depth)
    {
        Comment comment = Comment.Create(PostId, AuthorId, "content");
        while (comment.Depth < depth)
        {
            comment = comment.Reply(AuthorId, "content").Value;
        }

        return comment;
    }
}
