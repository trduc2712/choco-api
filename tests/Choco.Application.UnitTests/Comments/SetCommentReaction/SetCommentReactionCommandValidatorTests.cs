using Choco.Application.Comments.SetCommentReaction;
using Choco.Contract.Comments;

using FluentValidation.TestHelper;

namespace Choco.Application.UnitTests.Comments.SetCommentReaction;

public sealed class SetCommentReactionCommandValidatorTests
{
    private static readonly SetCommentReactionCommand ValidCommand = new(new Guid("00000000-0000-0000-0000-000000000001"), "like");

    private readonly SetCommentReactionCommandValidator _validator = new();

    [Theory]
    [InlineData("like")]
    [InlineData("dislike")]
    public void Validate_WithKnownType_HasNoErrors(string type)
    {
        SetCommentReactionCommand command = ValidCommand with { Type = type };

        TestValidationResult<SetCommentReactionCommand> result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithEmptyCommentId_HasCommentIdError()
    {
        SetCommentReactionCommand command = ValidCommand with { CommentId = Guid.Empty };

        TestValidationResult<SetCommentReactionCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.CommentId).Only();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Like")]
    [InlineData("type")]
    public void Validate_WithUnknownType_HasTypeError(string? type)
    {
        SetCommentReactionCommand command = ValidCommand with { Type = type! };

        TestValidationResult<SetCommentReactionCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.Type).Only();
    }
}
