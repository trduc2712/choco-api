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
        comment.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public void Create_WithValidData_RaisesCommentCreated()
    {
        Comment comment = Comment.Create(PostId, AuthorId, "content");

        comment.DomainEvents.Should().ContainSingle()
            .Which.Should().Be(new CommentCreated(comment.Id, PostId, AuthorId));
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
}
