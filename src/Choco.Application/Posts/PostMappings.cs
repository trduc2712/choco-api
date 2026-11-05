using Choco.Contract.Posts;
using Choco.Domain.Posts;

namespace Choco.Application.Posts;

internal static class PostMappings
{
    public static PostResponse ToResponse(this Post post)
    {
        return new PostResponse(post.Id, post.AuthorId, post.Content, post.CreatedAt, post.ModifiedAt);
    }
}
