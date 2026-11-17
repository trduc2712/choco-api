using Choco.Application.Comments.RemoveCommentReaction;
using Choco.Contract.Comments;

using FluentValidation.TestHelper;

namespace Choco.Application.UnitTests.Comments.RemoveCommentReaction;

public sealed class RemoveCommentReactionCommandValidatorTests
{
    private static readonly RemoveCommentReactionCommand ValidCommand = new(new Guid("00000000-0000-0000-0000-000000000001"));

    private readonly RemoveCommentReactionCommandValidator _validator = new();

    [Fact]
    public void Validate_WithCommentId_HasNoErrors()
    {
        TestValidationResult<RemoveCommentReactionCommand> result = _validator.TestValidate(ValidCommand);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithEmptyCommentId_HasCommentIdError()
    {
        RemoveCommentReactionCommand command = ValidCommand with { CommentId = Guid.Empty };

        TestValidationResult<RemoveCommentReactionCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.CommentId).Only();
    }
}
