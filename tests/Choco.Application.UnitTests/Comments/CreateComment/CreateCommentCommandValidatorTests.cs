using Choco.Application.Comments.CreateComment;
using Choco.Contract.Comments;
using Choco.Domain.Comments;

using FluentValidation.TestHelper;

namespace Choco.Application.UnitTests.Comments.CreateComment;

public sealed class CreateCommentCommandValidatorTests
{
    private static readonly CreateCommentCommand ValidCommand = new(new Guid("00000000-0000-0000-0000-000000000001"), "content");

    private readonly CreateCommentCommandValidator _validator = new();

    [Fact]
    public void Validate_WithValidCommand_HasNoErrors()
    {
        TestValidationResult<CreateCommentCommand> result = _validator.TestValidate(ValidCommand);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithEmptyPostId_HasPostIdError()
    {
        CreateCommentCommand command = ValidCommand with { PostId = Guid.Empty };

        TestValidationResult<CreateCommentCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.PostId).Only();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Validate_WithBlankContent_HasContentError(string? content)
    {
        CreateCommentCommand command = ValidCommand with { Content = content! };

        TestValidationResult<CreateCommentCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.Content).Only();
    }

    [Fact]
    public void Validate_WithContentAtMaxLength_HasNoErrors()
    {
        CreateCommentCommand command = ValidCommand with { Content = new string('c', Comment.ContentMaxLength) };

        TestValidationResult<CreateCommentCommand> result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithContentAtMaxLengthAfterTrimming_HasNoErrors()
    {
        CreateCommentCommand command = ValidCommand with { Content = $"  {new string('c', Comment.ContentMaxLength)}  " };

        TestValidationResult<CreateCommentCommand> result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithContentOverMaxLength_HasContentError()
    {
        CreateCommentCommand command = ValidCommand with { Content = new string('c', Comment.ContentMaxLength + 1) };

        TestValidationResult<CreateCommentCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.Content).Only();
    }
}
