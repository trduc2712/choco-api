using Choco.Application.Posts.UnlikePost;
using Choco.Contract.Posts;

using FluentValidation.TestHelper;

namespace Choco.Application.UnitTests.Posts.UnlikePost;

public sealed class UnlikePostCommandValidatorTests
{
    private static readonly UnlikePostCommand ValidCommand = new(new Guid("00000000-0000-0000-0000-000000000001"));

    private readonly UnlikePostCommandValidator _validator = new();

    [Fact]
    public void Validate_WithPostId_HasNoErrors()
    {
        TestValidationResult<UnlikePostCommand> result = _validator.TestValidate(ValidCommand);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithEmptyPostId_HasPostIdError()
    {
        UnlikePostCommand command = ValidCommand with { PostId = Guid.Empty };

        TestValidationResult<UnlikePostCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.PostId).Only();
    }
}
