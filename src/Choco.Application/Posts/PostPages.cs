using Choco.Contract.Abstractions;
using Choco.Contract.Posts;
using Choco.Domain.Posts;
using Choco.Domain.Users;

namespace Choco.Application.Posts;

internal static class PostPages
{
    public static async Task<PageResponse<PostResponse>> ReadByAuthorAsync(
        IPostRepository postRepository,
        IUserRepository userRepository,
        Guid authorId,
        string? cursor,
        int limit,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<Post> posts = await postRepository.ListByAuthorForReadAsync(
            authorId, ReadPosition(cursor), limit + 1, cancellationToken);

        return await ToPageAsync(posts, userRepository, limit, cancellationToken);
    }

    public static async Task<PageResponse<PostResponse>> ReadByFollowedAuthorsAsync(
        IPostRepository postRepository,
        IUserRepository userRepository,
        Guid followerId,
        string? cursor,
        int limit,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<Post> posts = await postRepository.ListByFollowedAuthorsForReadAsync(
            followerId, ReadPosition(cursor), limit + 1, cancellationToken);

        return await ToPageAsync(posts, userRepository, limit, cancellationToken);
    }

    public static async Task<PageResponse<PostResponse>> ReadRecommendedAsync(
        IPostRepository postRepository,
        IUserRepository userRepository,
        Guid viewerId,
        string? cursor,
        int limit,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<Post> posts = await postRepository.ListRecommendedForReadAsync(
            viewerId, ReadPosition(cursor), limit + 1, cancellationToken);

        return await ToPageAsync(posts, userRepository, limit, cancellationToken);
    }

    public static PageResponse<PostResponse> ToPage(
        IReadOnlyList<Post> posts, IReadOnlyCollection<User> authors, int limit)
    {
        Dictionary<Guid, User> authorsById = authors.ToDictionary(author => author.Id);
        List<Post> page = posts.Take(limit).ToList();
        string? nextCursor = posts.Count > limit
            ? new PostCursor(page[^1].CreatedAt, page[^1].Id).Encode()
            : null;

        List<PostResponse> items = page
            .Where(post => authorsById.ContainsKey(post.AuthorId))
            .Select(post => post.ToResponse(authorsById[post.AuthorId]))
            .ToList();

        return new PageResponse<PostResponse>(items, nextCursor);
    }

    private static async Task<PageResponse<PostResponse>> ToPageAsync(
        IReadOnlyList<Post> posts,
        IUserRepository userRepository,
        int limit,
        CancellationToken cancellationToken)
    {
        Guid[] authorIds = posts.Take(limit).Select(post => post.AuthorId).Distinct().ToArray();
        IReadOnlyList<User> authors = authorIds.Length == 0
            ? []
            : await userRepository.ListByIdsForReadAsync(authorIds, cancellationToken);

        return ToPage(posts, authors, limit);
    }

    private static (DateTimeOffset CreatedAt, Guid Id)? ReadPosition(string? cursor)
    {
        return PostCursor.TryDecode(cursor, out PostCursor? position)
            ? (position!.CreatedAt, position.Id)
            : null;
    }
}
