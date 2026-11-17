using Choco.Contract.Abstractions;
using Choco.Contract.Posts;
using Choco.Domain.Comments;
using Choco.Domain.PostLikes;
using Choco.Domain.Posts;
using Choco.Domain.Users;

namespace Choco.Application.Posts;

internal static class PostPages
{
    public static PageResponse<PostResponse> ToPage(
        IReadOnlyList<Post> posts,
        IReadOnlyCollection<User> users,
        IReadOnlyDictionary<Guid, PostLikeSummary> likes,
        IReadOnlyDictionary<Guid, int> commentCounts,
        IReadOnlyDictionary<Guid, Comment> topComments,
        int limit)
    {
        Dictionary<Guid, User> usersById = users.ToDictionary(user => user.Id);
        List<Post> page = posts.Take(limit).ToList();
        string? nextCursor = posts.Count > limit
            ? new PostCursor(page[^1].CreatedAt, page[^1].Id).Encode()
            : null;

        List<PostResponse> items = page
            .Where(post => usersById.ContainsKey(post.AuthorId))
            .Select(post => post.ToResponse(
                usersById[post.AuthorId],
                likes.GetValueOrDefault(post.Id, PostLikeSummary.None),
                commentCounts.GetValueOrDefault(post.Id),
                topComments.GetValueOrDefault(post.Id).ToTopCommentResponse(usersById)))
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
