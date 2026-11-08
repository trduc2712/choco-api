using Choco.Contract.Users;

using FluentValidation;

namespace Choco.Application.Users.GetUserFollowing;

internal sealed class GetUserFollowingQueryValidator : AbstractValidator<GetUserFollowingQuery>
{
    public GetUserFollowingQueryValidator()
    {
        RuleFor(query => query.UserId).NotEmpty();
        RuleFor(query => query.Limit).InclusiveBetween(1, GetUserFollowersQuery.MaxLimit);

        RuleFor(query => query.Cursor)
            .Must(cursor => FollowCursor.TryDecode(cursor, out _))
            .When(query => query.Cursor is not null)
            .WithMessage("'{PropertyName}' is not a valid cursor.");
    }
}
