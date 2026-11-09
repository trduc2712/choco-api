using Choco.Contract.Comments;
using Choco.Contract.Posts;
using Choco.Domain.Comments;
using Choco.Domain.Users;

namespace Choco.Application.Comments;

internal static class CommentMappings
{
    public static CommentResponse ToResponse(this Comment comment, User author)
    {
        return new CommentResponse(
            comment.Id,
            comment.PostId,
            comment.AuthorId,
            new PostAuthorResponse(author.Username, author.DisplayName, author.AvatarUrl),
            comment.Content,
            comment.CreatedAt);
    }
}
