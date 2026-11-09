using Choco.Contract.Posts;
using Choco.Domain.PostLikes;
using Choco.Domain.Posts;
using Choco.Domain.Users;

namespace Choco.Application.Posts;

internal static class PostMappings
{
    public static PostResponse ToResponse(this Post post, User author, PostLikeSummary likes, int commentCount)
    {
        return new PostResponse(
            post.Id,
            post.AuthorId,
            new PostAuthorResponse(author.Username, author.DisplayName, author.AvatarUrl),
            post.Content,
            likes.LikeCount,
            likes.IsLikedByViewer,
            commentCount,
            post.CreatedAt,
            post.ModifiedAt);
    }
}
