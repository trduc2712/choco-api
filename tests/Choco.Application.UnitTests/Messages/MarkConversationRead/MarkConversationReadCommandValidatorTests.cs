using Choco.Application.Messages.MarkConversationRead;
using Choco.Contract.Messages;

using FluentValidation.TestHelper;

namespace Choco.Application.UnitTests.Messages.MarkConversationRead;

public sealed class MarkConversationReadCommandValidatorTests
{
    private static readonly MarkConversationReadCommand Valid = new(0) { ConversationId = new Guid("00000000-0000-0000-0000-000000000001") };
    private static readonly MarkConversationReadCommandValidator Validator = new();

    [Fact]
    public void Validate_EmptyConversationId_RejectsConversationId()
    {
        Validator.TestValidate(Valid with { ConversationId = Guid.Empty })
            .ShouldHaveValidationErrorFor(command => command.ConversationId).Only();
    }

    [Fact]
    public void Validate_NegativeSequence_RejectsSequence()
    {
        Validator.TestValidate(Valid with { Sequence = -1 }).ShouldHaveValidationErrorFor(command => command.Sequence).Only();
    }

    [Fact]
    public void Validate_SequenceZero_AcceptsCommand()
    {
        Validator.TestValidate(Valid).ShouldNotHaveAnyValidationErrors();
    }
}
