using Choco.Application.Messages.SendMessage;
using Choco.Contract.Messages;
using Choco.Domain.Conversations;

using FluentValidation.TestHelper;

namespace Choco.Application.UnitTests.Messages.SendMessage;

public sealed class SendMessageCommandValidatorTests
{
    private static readonly SendMessageCommand ValidCommand = new(
        new Guid("00000000-0000-0000-0000-000000000001"),
        new Guid("4b6f2c1a-8d3e-4f5a-9b7c-1e2d3f4a5b6c"),
        "content");

    private readonly SendMessageCommandValidator _validator = new();

    [Fact]
    public void Validate_WithValidCommand_HasNoErrors()
    {
        TestValidationResult<SendMessageCommand> result = _validator.TestValidate(ValidCommand);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithEmptyRecipientId_HasRecipientIdError()
    {
        SendMessageCommand command = ValidCommand with { RecipientId = Guid.Empty };

        TestValidationResult<SendMessageCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.RecipientId).Only();
    }

    [Fact]
    public void Validate_WithEmptyClientMessageId_HasClientMessageIdError()
    {
        SendMessageCommand command = ValidCommand with { ClientMessageId = Guid.Empty };

        TestValidationResult<SendMessageCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.ClientMessageId).Only();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Validate_WithBlankContent_HasContentError(string? content)
    {
        SendMessageCommand command = ValidCommand with { Content = content! };

        TestValidationResult<SendMessageCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.Content).Only();
    }

    [Fact]
    public void Validate_WithContentAtMaxLengthAfterTrimming_HasNoErrors()
    {
        SendMessageCommand command = ValidCommand with { Content = $"  {new string('c', Message.ContentMaxLength)}  " };

        TestValidationResult<SendMessageCommand> result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithContentOverMaxLength_HasContentError()
    {
        SendMessageCommand command = ValidCommand with { Content = new string('c', Message.ContentMaxLength + 1) };

        TestValidationResult<SendMessageCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.Content).Only();
    }
}
