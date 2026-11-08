using Choco.Contract.Posts;

using FluentValidation;

namespace Choco.Application.Posts.GetFollowingFeed;

internal sealed class GetFollowingFeedQueryValidator : AbstractValidator<GetFollowingFeedQuery>
{
    public GetFollowingFeedQueryValidator()
    {
        RuleFor(query => query.Limit).InclusiveBetween(1, GetMyPostsQuery.MaxLimit);

        RuleFor(query => query.Cursor)
            .Must(cursor => PostCursor.TryDecode(cursor, out _))
            .When(query => query.Cursor is not null)
            .WithMessage("'{PropertyName}' is not a valid cursor.");
    }
}
