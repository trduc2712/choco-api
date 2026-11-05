using Choco.Application.Posts.EditPost;
using Choco.Contract.Posts;
using Choco.Domain.Posts;

using FluentValidation.TestHelper;

namespace Choco.Application.UnitTests.Posts.EditPost;

public sealed class EditPostCommandValidatorTests
{
    private static readonly EditPostCommand ValidCommand = new(new Guid("4d5e6f7a-8b9c-4d0e-9f1a-2b3c4d5e6f7a"), "content");

    private readonly EditPostCommandValidator _validator = new();

    [Fact]
    public void Validate_WithValidCommand_HasNoErrors()
    {
        TestValidationResult<EditPostCommand> result = _validator.TestValidate(ValidCommand);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithContentAtMaxLengthAfterTrimming_HasNoErrors()
    {
        EditPostCommand command = ValidCommand with { Content = $"  {new string('c', Post.ContentMaxLength)}  " };

        TestValidationResult<EditPostCommand> result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithContentOverMaxLength_HasContentError()
    {
        EditPostCommand command = ValidCommand with { Content = new string('c', Post.ContentMaxLength + 1) };

        TestValidationResult<EditPostCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.Content).Only();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Validate_WithBlankContent_HasContentError(string? content)
    {
        EditPostCommand command = ValidCommand with { Content = content! };

        TestValidationResult<EditPostCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.Content).Only();
    }

    [Fact]
    public void Validate_WithEmptyId_HasIdError()
    {
        EditPostCommand command = ValidCommand with { Id = Guid.Empty };

        TestValidationResult<EditPostCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.Id).Only();
    }
}
