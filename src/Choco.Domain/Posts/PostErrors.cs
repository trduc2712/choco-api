using Choco.SharedKernel.Results;

namespace Choco.Domain.Posts;

public static class PostErrors
{
    public static readonly Error NotFound = Error.NotFound("Post.NotFound", "The post was not found.");
    public static readonly Error NotAuthor = Error.Forbidden("Post.NotAuthor", "Only the author can change or delete this post.");
}
