using Choco.Contract.Abstractions;
using Choco.Contract.Posts;
using Choco.Domain.PostLikes;
using Choco.Domain.Posts;
using Choco.Domain.Users;

namespace Choco.Application.Posts;

internal static class PostPages
{
    public static PageResponse<PostResponse> ToPage(
        IReadOnlyList<Post> posts,
        IReadOnlyCollection<User> authors,
        IReadOnlyDictionary<Guid, PostLikeSummary> likes,
        IReadOnlyDictionary<Guid, int> commentCounts,
        int limit)
    {
        Dictionary<Guid, User> authorsById = authors.ToDictionary(author => author.Id);
        List<Post> page = posts.Take(limit).ToList();
        string? nextCursor = posts.Count > limit
            ? new PostCursor(page[^1].CreatedAt, page[^1].Id).Encode()
            : null;

        List<PostResponse> items = page
            .Where(post => authorsById.ContainsKey(post.AuthorId))
            .Select(post => post.ToResponse(
                authorsById[post.AuthorId],
                likes.GetValueOrDefault(post.Id, PostLikeSummary.None),
                commentCounts.GetValueOrDefault(post.Id)))
            .ToList();

        return new PageResponse<PostResponse>(items, nextCursor);
    }

    public static (DateTimeOffset CreatedAt, Guid Id)? ReadPosition(string? cursor)
    {
        return PostCursor.TryDecode(cursor, out PostCursor? position)
            ? (position!.CreatedAt, position.Id)
            : null;
    }
}
