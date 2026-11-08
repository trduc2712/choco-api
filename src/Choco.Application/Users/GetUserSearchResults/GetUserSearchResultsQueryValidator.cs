using Choco.Contract.Users;
using Choco.Domain.Users;

using FluentValidation;

namespace Choco.Application.Users.GetUserSearchResults;

internal sealed class GetUserSearchResultsQueryValidator : AbstractValidator<GetUserSearchResultsQuery>
{
    public GetUserSearchResultsQueryValidator()
    {
        RuleFor(query => query.Q)
            .Must(UserSearchText.IsValid)
            .WithMessage($"Search text must contain at most {User.DisplayNameMaxLength} characters after trimming and no null characters.");

        RuleFor(query => query.Cursor)
            .Must((query, cursor) => UserSearchCursor.TryDecode(cursor, query.Q?.Trim() ?? string.Empty, out _))
            .When(query => query.Cursor is not null)
            .WithMessage("Use a cursor returned for the same search text.");
    }
}
