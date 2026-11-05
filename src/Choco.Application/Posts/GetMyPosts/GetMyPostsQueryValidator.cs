using Choco.Contract.Posts;

using FluentValidation;

namespace Choco.Application.Posts.GetMyPosts;

internal sealed class GetMyPostsQueryValidator : AbstractValidator<GetMyPostsQuery>
{
    public GetMyPostsQueryValidator()
    {
        RuleFor(query => query.Limit).InclusiveBetween(1, GetMyPostsQuery.MaxLimit);

        RuleFor(query => query.Cursor)
            .Must(cursor => PostCursor.TryDecode(cursor, out _))
            .When(query => query.Cursor is not null)
            .WithMessage("'{PropertyName}' is not a valid cursor.");
    }
}
