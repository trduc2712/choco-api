using Choco.Contract.Messages;

using FluentValidation;

namespace Choco.Application.Messages.MarkConversationRead;

internal sealed class MarkConversationReadCommandValidator : AbstractValidator<MarkConversationReadCommand>
{
    public MarkConversationReadCommandValidator()
    {
        RuleFor(command => command.ConversationId).NotEmpty();
        RuleFor(command => command.Sequence).GreaterThanOrEqualTo(0);
    }
}
