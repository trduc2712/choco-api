using Choco.Contract.Comments;
using Choco.Contract.Posts;
using Choco.Domain.CommentReactions;
using Choco.Domain.Comments;
using Choco.Domain.Users;

namespace Choco.Application.Comments;

internal static class CommentMappings
{
    public static CommentResponse ToResponse(
        this Comment comment, User author, int replyCount = 0, CommentReactionSummary? reactions = null)
    {
        CommentReactionSummary summary = reactions ?? CommentReactionSummary.None;

        return new CommentResponse(
            comment.Id,
            comment.PostId,
            comment.ParentId,
            comment.AuthorId,
            new PostAuthorResponse(author.Username, author.DisplayName, author.AvatarUrl),
            comment.Content,
            replyCount,
            summary.LikeCount,
            summary.DislikeCount,
            summary.ViewerReaction is { } type ? CommentReactionTypes.ToWire(type) : null,
            false,
            comment.CreatedAt);
    }

    public static CommentResponse ToPlaceholder(this Comment comment, int replyCount)
    {
        return new CommentResponse(
            comment.Id,
            comment.PostId,
            comment.ParentId,
            null,
            null,
            null,
            replyCount,
            0,
            0,
            null,
            true,
            comment.CreatedAt);
    }
}
