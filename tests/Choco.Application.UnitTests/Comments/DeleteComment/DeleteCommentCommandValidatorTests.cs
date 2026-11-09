using Choco.Application.Comments.DeleteComment;
using Choco.Contract.Comments;

using FluentValidation.TestHelper;

namespace Choco.Application.UnitTests.Comments.DeleteComment;

public sealed class DeleteCommentCommandValidatorTests
{
    private static readonly DeleteCommentCommand ValidCommand = new(new Guid("00000000-0000-0000-0000-000000000001"));

    private readonly DeleteCommentCommandValidator _validator = new();

    [Fact]
    public void Validate_WithCommentId_HasNoErrors()
    {
        TestValidationResult<DeleteCommentCommand> result = _validator.TestValidate(ValidCommand);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithEmptyCommentId_HasCommentIdError()
    {
        DeleteCommentCommand command = ValidCommand with { CommentId = Guid.Empty };

        TestValidationResult<DeleteCommentCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.CommentId).Only();
    }
}
