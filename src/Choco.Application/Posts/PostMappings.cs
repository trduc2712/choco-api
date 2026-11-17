using Choco.Contract.Posts;
using Choco.Domain.Comments;
using Choco.Domain.PostLikes;
using Choco.Domain.Posts;
using Choco.Domain.Users;

namespace Choco.Application.Posts;

internal static class PostMappings
{
    public static PostResponse ToResponse(
        this Post post, User author, PostLikeSummary likes, int commentCount, PostTopCommentResponse? topComment)
    {
        return new PostResponse(
            post.Id,
            post.AuthorId,
            author.ToPostAuthorResponse(),
            post.Content,
            likes.LikeCount,
            likes.IsLikedByViewer,
            commentCount,
            post.CreatedAt,
            post.ModifiedAt,
            topComment);
    }

    public static PostTopCommentResponse? ToTopCommentResponse(
        this Comment? comment, IReadOnlyDictionary<Guid, User> usersById)
    {
        return comment is not null && usersById.TryGetValue(comment.AuthorId, out User? author)
            ? new PostTopCommentResponse(comment.Id, author.ToPostAuthorResponse(), comment.Content)
            : null;
    }

    private static PostAuthorResponse ToPostAuthorResponse(this User user)
    {
        return new PostAuthorResponse(user.Username, user.DisplayName, user.AvatarUrl);
    }
}
