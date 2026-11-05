using Choco.Domain.Posts;
using Choco.SharedKernel.Results;

namespace Choco.Domain.UnitTests.Posts;

public sealed class PostTests
{
    private static readonly Guid AuthorId = new("5f0c6b1e-2d4a-4c8e-9b7a-3e1f2a6d8c40");

    [Fact]
    public void Create_WithValidData_ReturnsPostWithThatData()
    {
        string content = "content";

        Post post = Post.Create(content, AuthorId);

        post.Id.Should().NotBeEmpty();
        post.AuthorId.Should().Be(AuthorId);
        post.Content.Should().Be(content);
        post.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public void Create_WithSurroundingWhitespace_StoresContentTrimmedAtBothEnds()
    {
        Post post = Post.Create(" \n\tcontent-line-1\n\n  content-line-2 \t\n", AuthorId);

        post.Content.Should().Be("content-line-1\n\n  content-line-2");
    }

    [Fact]
    public void Create_WithContentAtMaxLength_ReturnsPostWithThatContent()
    {
        string content = new('c', Post.ContentMaxLength);

        Post post = Post.Create(content, AuthorId);

        post.Content.Should().Be(content);
    }

    [Fact]
    public void Create_WithContentAtMaxLengthAfterTrimming_ReturnsPostWithTrimmedContent()
    {
        string trimmedContent = new('c', Post.ContentMaxLength);

        Post post = Post.Create($"  {trimmedContent}  ", AuthorId);

        post.Content.Should().Be(trimmedContent);
    }

    [Fact]
    public void Create_WithContentOverMaxLength_ThrowsArgumentException()
    {
        string content = new('c', Post.ContentMaxLength + 1);

        Func<Post> act = () => Post.Create(content, AuthorId);

        act.Should().Throw<ArgumentException>().WithParameterName("content");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Create_WithBlankContent_ThrowsArgumentException(string? content)
    {
        Func<Post> act = () => Post.Create(content!, AuthorId);

        act.Should().Throw<ArgumentException>().WithParameterName("content");
    }

    [Fact]
    public void Create_WithEmptyAuthorId_ThrowsArgumentException()
    {
        Func<Post> act = () => Post.Create("content", Guid.Empty);

        act.Should().Throw<ArgumentException>().WithParameterName("authorId");
    }

    [Fact]
    public void Edit_WithValidContent_ReplacesContentOnly()
    {
        Post post = Post.Create("content", AuthorId);
        Guid id = post.Id;

        post.Edit("edited-content", AuthorId);

        post.Content.Should().Be("edited-content");
        post.Id.Should().Be(id);
        post.AuthorId.Should().Be(AuthorId);
    }

    [Fact]
    public void Edit_WithSurroundingWhitespace_StoresContentTrimmedAtBothEnds()
    {
        Post post = Post.Create("content", AuthorId);

        post.Edit(" \n\tedited-content-line-1\n\n  edited-content-line-2 \t\n", AuthorId);

        post.Content.Should().Be("edited-content-line-1\n\n  edited-content-line-2");
    }

    [Fact]
    public void Edit_WithContentAtMaxLength_ReplacesContent()
    {
        Post post = Post.Create("content", AuthorId);
        string editedContent = new('e', Post.ContentMaxLength);

        post.Edit(editedContent, AuthorId);

        post.Content.Should().Be(editedContent);
    }

    [Fact]
    public void Edit_WithContentOverMaxLength_ThrowsArgumentExceptionAndKeepsContent()
    {
        Post post = Post.Create("content", AuthorId);
        string editedContent = new('e', Post.ContentMaxLength + 1);

        Action act = () => post.Edit(editedContent, AuthorId);

        act.Should().Throw<ArgumentException>().WithParameterName("content");
        post.Content.Should().Be("content");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Edit_WithBlankContent_ThrowsArgumentExceptionAndKeepsContent(string? editedContent)
    {
        Post post = Post.Create("content", AuthorId);

        Action act = () => post.Edit(editedContent!, AuthorId);

        act.Should().Throw<ArgumentException>().WithParameterName("content");
        post.Content.Should().Be("content");
    }

    [Fact]
    public void Edit_ByAnotherUser_ReturnsNotAuthorAndKeepsContent()
    {
        Post post = Post.Create("content", AuthorId);
        Guid otherUserId = new("6a7b8c9d-0e1f-4a2b-8c3d-4e5f6a7b8c9d");

        Result result = post.Edit("edited-content", otherUserId);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(PostErrors.NotAuthor);
        post.Content.Should().Be("content");
    }

    [Fact]
    public void Edit_ByAuthor_ReturnsSuccess()
    {
        Post post = Post.Create("content", AuthorId);

        Result result = post.Edit("edited-content", AuthorId);

        result.IsSuccess.Should().BeTrue();
    }
}
