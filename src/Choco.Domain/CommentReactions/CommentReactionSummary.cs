namespace Choco.Domain.CommentReactions;

public sealed record CommentReactionSummary(int LikeCount, int DislikeCount, CommentReactionType? ViewerReaction)
{
    public static readonly CommentReactionSummary None = new(0, 0, null);
}
