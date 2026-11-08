using Choco.Contract.Users;

using FluentValidation;

namespace Choco.Application.Users.GetUserFollowers;

internal sealed class GetUserFollowersQueryValidator : AbstractValidator<GetUserFollowersQuery>
{
    public GetUserFollowersQueryValidator()
    {
        RuleFor(query => query.UserId).NotEmpty();
        RuleFor(query => query.Limit).InclusiveBetween(1, GetUserFollowersQuery.MaxLimit);
        RuleFor(query => query.Cursor)
            .Must(cursor => Guid.TryParseExact(cursor, "N", out _))
            .When(query => query.Cursor is not null)
            .WithMessage("'{PropertyName}' is not a valid cursor.");
    }
}
