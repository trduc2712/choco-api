using Choco.Contract.Abstractions;
using Choco.Contract.Posts;
using Choco.Domain.Posts;

namespace Choco.Application.Posts;

internal static class PostPages
{
    public static async Task<PageResponse<PostResponse>> ReadByAuthorAsync(
        IPostRepository postRepository,
        Guid authorId,
        string? cursor,
        int limit,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<Post> posts = await postRepository.ListByAuthorForReadAsync(
            authorId, ReadPosition(cursor), limit + 1, cancellationToken);

        return ToPage(posts, limit);
    }

    public static async Task<PageResponse<PostResponse>> ReadByFollowedAuthorsAsync(
        IPostRepository postRepository,
        Guid followerId,
        string? cursor,
        int limit,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<Post> posts = await postRepository.ListByFollowedAuthorsForReadAsync(
            followerId, ReadPosition(cursor), limit + 1, cancellationToken);

        return ToPage(posts, limit);
    }

    public static async Task<PageResponse<PostResponse>> ReadRecommendedAsync(
        IPostRepository postRepository,
        Guid viewerId,
        string? cursor,
        int limit,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<Post> posts = await postRepository.ListRecommendedForReadAsync(
            viewerId, ReadPosition(cursor), limit + 1, cancellationToken);

        return ToPage(posts, limit);
    }

    public static PageResponse<PostResponse> ToPage(IReadOnlyList<Post> posts, int limit)
    {
        List<Post> page = posts.Take(limit).ToList();
        string? nextCursor = posts.Count > limit
            ? new PostCursor(page[^1].CreatedAt, page[^1].Id).Encode()
            : null;

        return new PageResponse<PostResponse>(page.Select(post => post.ToResponse()).ToList(), nextCursor);
    }

    private static (DateTimeOffset CreatedAt, Guid Id)? ReadPosition(string? cursor)
    {
        return PostCursor.TryDecode(cursor, out PostCursor? position)
            ? (position!.CreatedAt, position.Id)
            : null;
    }
}
