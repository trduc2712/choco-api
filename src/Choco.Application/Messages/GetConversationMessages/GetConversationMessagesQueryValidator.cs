using Choco.Contract.Messages;

using FluentValidation;

namespace Choco.Application.Messages.GetConversationMessages;

internal sealed class GetConversationMessagesQueryValidator : AbstractValidator<GetConversationMessagesQuery>
{
    public GetConversationMessagesQueryValidator()
    {
        RuleFor(query => query.ConversationId).NotEmpty();
        RuleFor(query => query.Limit).InclusiveBetween(1, GetConversationsQuery.MaxLimit);
        RuleFor(query => query.AfterSequence).GreaterThanOrEqualTo(0);
        RuleFor(query => query.Cursor)
            .Cascade(CascadeMode.Stop)
            .Null().When(query => query.AfterSequence.HasValue, ApplyConditionTo.CurrentValidator)
            .WithMessage("Supply either cursor or afterSequence.")
            .Must(cursor => MessageCursor.TryDecode(cursor, out _))
            .When(query => query.Cursor is not null)
            .WithMessage("'{PropertyName}' is not a valid cursor.");
    }
}
