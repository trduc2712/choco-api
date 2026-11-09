using Choco.Application.Posts;
using Choco.Contract.Abstractions;
using Choco.Contract.Comments;
using Choco.Domain.Comments;
using Choco.Domain.Users;

namespace Choco.Application.Comments;

internal static class CommentPages
{
    public static PageResponse<CommentResponse> ToPage(
        IReadOnlyList<Comment> comments, IReadOnlyCollection<User> authors, int limit)
    {
        Dictionary<Guid, User> authorsById = authors.ToDictionary(author => author.Id);
        List<Comment> page = comments.Take(limit).ToList();
        string? nextCursor = comments.Count > limit
            ? new PostCursor(page[^1].CreatedAt, page[^1].Id).Encode()
            : null;

        List<CommentResponse> items = page
            .Where(comment => authorsById.ContainsKey(comment.AuthorId))
            .Select(comment => comment.ToResponse(authorsById[comment.AuthorId]))
            .ToList();

        return new PageResponse<CommentResponse>(items, nextCursor);
    }
}
