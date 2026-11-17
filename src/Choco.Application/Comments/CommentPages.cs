using Choco.Application.Posts;
using Choco.Contract.Abstractions;
using Choco.Contract.Comments;
using Choco.Domain.Comments;
using Choco.Domain.Users;

namespace Choco.Application.Comments;

internal static class CommentPages
{
    public static PageResponse<CommentResponse> ToPage(
        IReadOnlyList<CommentThreadEntry> entries, IReadOnlyCollection<User> authors, int limit)
    {
        Dictionary<Guid, User> authorsById = authors.ToDictionary(author => author.Id);
        List<CommentThreadEntry> page = entries.Take(limit).ToList();
        string? nextCursor = entries.Count > limit
            ? new PostCursor(page[^1].Comment.CreatedAt, page[^1].Comment.Id).Encode()
            : null;

        List<CommentResponse> items = page
            .Where(entry => !entry.IsVisible || authorsById.ContainsKey(entry.Comment.AuthorId))
            .Select(entry => entry.IsVisible
                ? entry.Comment.ToResponse(authorsById[entry.Comment.AuthorId], entry.ReplyCount)
                : entry.Comment.ToPlaceholder(entry.ReplyCount))
            .ToList();

        return new PageResponse<CommentResponse>(items, nextCursor);
    }
}
