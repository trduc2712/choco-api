using Choco.Application.Posts;
using Choco.Contract.Comments;
using Choco.Contract.Posts;

using FluentValidation;

namespace Choco.Application.Comments.GetCommentReplies;

internal sealed class GetCommentRepliesQueryValidator : AbstractValidator<GetCommentRepliesQuery>
{
    public GetCommentRepliesQueryValidator()
    {
        RuleFor(query => query.CommentId).NotEmpty();
        RuleFor(query => query.Limit).InclusiveBetween(1, GetMyPostsQuery.MaxLimit);

        RuleFor(query => query.Cursor)
            .Must(cursor => PostCursor.TryDecode(cursor, out _))
            .When(query => query.Cursor is not null)
            .WithMessage("'{PropertyName}' is not a valid cursor.");
    }
}
