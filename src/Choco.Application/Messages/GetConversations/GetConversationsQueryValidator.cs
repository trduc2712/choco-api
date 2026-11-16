using Choco.Contract.Messages;

using FluentValidation;

namespace Choco.Application.Messages.GetConversations;

internal sealed class GetConversationsQueryValidator : AbstractValidator<GetConversationsQuery>
{
    public GetConversationsQueryValidator()
    {
        RuleFor(query => query.Limit).InclusiveBetween(1, GetConversationsQuery.MaxLimit);
        RuleFor(query => query.Cursor)
            .Must(cursor => ConversationCursor.TryDecode(cursor, out _))
            .When(query => query.Cursor is not null)
            .WithMessage("'{PropertyName}' is not a valid cursor.");
    }
}
