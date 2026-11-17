using Choco.Application.Comments.ReplyToComment;
using Choco.Contract.Comments;
using Choco.Domain.Comments;

using FluentValidation.TestHelper;

namespace Choco.Application.UnitTests.Comments.ReplyToComment;

public sealed class ReplyToCommentCommandValidatorTests
{
    private static readonly ReplyToCommentCommand ValidCommand = new(new Guid("00000000-0000-0000-0000-000000000001"), "content");

    private readonly ReplyToCommentCommandValidator _validator = new();

    [Fact]
    public void Validate_WithValidCommand_HasNoErrors()
    {
        TestValidationResult<ReplyToCommentCommand> result = _validator.TestValidate(ValidCommand);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithEmptyCommentId_HasCommentIdError()
    {
        ReplyToCommentCommand command = ValidCommand with { CommentId = Guid.Empty };

        TestValidationResult<ReplyToCommentCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.CommentId).Only();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Validate_WithBlankContent_HasContentError(string? content)
    {
        ReplyToCommentCommand command = ValidCommand with { Content = content! };

        TestValidationResult<ReplyToCommentCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.Content).Only();
    }

    [Fact]
    public void Validate_WithContentAtMaxLength_HasNoErrors()
    {
        ReplyToCommentCommand command = ValidCommand with { Content = new string('c', Comment.ContentMaxLength) };

        TestValidationResult<ReplyToCommentCommand> result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithContentAtMaxLengthAfterTrimming_HasNoErrors()
    {
        ReplyToCommentCommand command = ValidCommand with { Content = $"  {new string('c', Comment.ContentMaxLength)}  " };

        TestValidationResult<ReplyToCommentCommand> result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithContentOverMaxLength_HasContentError()
    {
        ReplyToCommentCommand command = ValidCommand with { Content = new string('c', Comment.ContentMaxLength + 1) };

        TestValidationResult<ReplyToCommentCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.Content).Only();
    }
}
