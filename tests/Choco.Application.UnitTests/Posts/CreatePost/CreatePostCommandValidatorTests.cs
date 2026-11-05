using Choco.Application.Posts.CreatePost;
using Choco.Contract.Posts;
using Choco.Domain.Posts;

using FluentValidation.TestHelper;

namespace Choco.Application.UnitTests.Posts.CreatePost;

public sealed class CreatePostCommandValidatorTests
{
    private static readonly CreatePostCommand ValidCommand = new("content");

    private readonly CreatePostCommandValidator _validator = new();

    [Fact]
    public void Validate_WithValidCommand_HasNoErrors()
    {
        TestValidationResult<CreatePostCommand> result = _validator.TestValidate(ValidCommand);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithContentAtMaxLength_HasNoErrors()
    {
        CreatePostCommand command = ValidCommand with { Content = new string('c', Post.ContentMaxLength) };

        TestValidationResult<CreatePostCommand> result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithContentAtMaxLengthAfterTrimming_HasNoErrors()
    {
        CreatePostCommand command = ValidCommand with { Content = $"  {new string('c', Post.ContentMaxLength)}  " };

        TestValidationResult<CreatePostCommand> result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithContentOverMaxLength_HasContentError()
    {
        CreatePostCommand command = ValidCommand with { Content = new string('c', Post.ContentMaxLength + 1) };

        TestValidationResult<CreatePostCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.Content).Only();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Validate_WithBlankContent_HasContentError(string? content)
    {
        CreatePostCommand command = ValidCommand with { Content = content! };

        TestValidationResult<CreatePostCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.Content).Only();
    }
}
