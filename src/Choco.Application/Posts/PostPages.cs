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
        (DateTimeOffset CreatedAt, Guid Id)? after = PostCursor.TryDecode(cursor, out PostCursor? position)
            ? (position!.CreatedAt, position.Id)
            : null;

        IReadOnlyList<Post> posts = await postRepository.ListByAuthorForReadAsync(
            authorId, after, limit + 1, cancellationToken);

        List<Post> page = posts.Take(limit).ToList();
        string? nextCursor = posts.Count > limit
            ? new PostCursor(page[^1].CreatedAt, page[^1].Id).Encode()
            : null;

        return new PageResponse<PostResponse>(page.Select(post => post.ToResponse()).ToList(), nextCursor);
    }
}
