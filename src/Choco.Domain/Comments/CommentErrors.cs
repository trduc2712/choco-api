using Choco.SharedKernel.Results;

namespace Choco.Domain.Comments;

public static class CommentErrors
{
    public static readonly Error NotFound = Error.NotFound("Comment.NotFound", "The comment was not found.");
    public static readonly Error NotAuthor = Error.Forbidden("Comment.NotAuthor", "Only the author can delete this comment.");
}
