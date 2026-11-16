using Choco.Contract.Messages;

using FluentValidation;

namespace Choco.Application.Messages.GetConversationByUser;

internal sealed class GetConversationByUserQueryValidator : AbstractValidator<GetConversationByUserQuery>
{
    public GetConversationByUserQueryValidator()
    {
        RuleFor(query => query.UserId).NotEmpty();
    }
}
