using Choco.Application.Posts.LikePost;
using Choco.Contract.Posts;

using FluentValidation.TestHelper;

namespace Choco.Application.UnitTests.Posts.LikePost;

public sealed class LikePostCommandValidatorTests
{
    private static readonly LikePostCommand ValidCommand = new(new Guid("00000000-0000-0000-0000-000000000001"));

    private readonly LikePostCommandValidator _validator = new();

    [Fact]
    public void Validate_WithPostId_HasNoErrors()
    {
        TestValidationResult<LikePostCommand> result = _validator.TestValidate(ValidCommand);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithEmptyPostId_HasPostIdError()
    {
        LikePostCommand command = ValidCommand with { PostId = Guid.Empty };

        TestValidationResult<LikePostCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.PostId).Only();
    }
}
