namespace Choco.Domain.PostLikes;

public sealed record PostLikeSummary(int LikeCount, bool IsLikedByViewer)
{
    public static readonly PostLikeSummary None = new(0, false);
}
