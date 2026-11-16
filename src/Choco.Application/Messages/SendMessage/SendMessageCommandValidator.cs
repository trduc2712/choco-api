using Choco.Contract.Messages;
using Choco.Domain.Conversations;

using FluentValidation;

namespace Choco.Application.Messages.SendMessage;

internal sealed class SendMessageCommandValidator : AbstractValidator<SendMessageCommand>
{
    public SendMessageCommandValidator()
    {
        RuleFor(command => command.RecipientId).NotEmpty();

        RuleFor(command => command.ClientMessageId).NotEmpty();

        RuleFor(command => command.Content)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .Must(content => content.Trim().Length <= Message.ContentMaxLength)
            .WithErrorCode("TooLong")
            .WithMessage($"'{{PropertyName}}' must be {Message.ContentMaxLength} characters or fewer once leading and trailing whitespace is removed.");
    }
}
