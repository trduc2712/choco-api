using Choco.Contract.Users;
using Choco.Domain.Users;

using FluentValidation;

namespace Choco.Application.Users.GetUserSuggestions;

internal sealed class GetUserSuggestionsQueryValidator : AbstractValidator<GetUserSuggestionsQuery>
{
    public GetUserSuggestionsQueryValidator()
    {
        RuleFor(query => query.Q)
            .Must(UserSearchText.IsValid)
            .WithMessage($"Search text must contain at most {User.DisplayNameMaxLength} characters after trimming and no null characters.");
    }
}
