using Choco.Contract.Comments;
using Choco.Contract.Posts;
using Choco.Domain.Comments;
using Choco.Domain.Users;

namespace Choco.Application.Comments;

internal static class CommentMappings
{
    public static CommentResponse ToResponse(this Comment comment, User author, int replyCount = 0)
    {
        return new CommentResponse(
            comment.Id,
            comment.PostId,
            comment.ParentId,
            comment.AuthorId,
            new PostAuthorResponse(author.Username, author.DisplayName, author.AvatarUrl),
            comment.Content,
            replyCount,
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
            true,
            comment.CreatedAt);
    }
}
