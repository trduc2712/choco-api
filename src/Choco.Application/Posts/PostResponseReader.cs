using Choco.Contract.Abstractions;
using Choco.Contract.Posts;
using Choco.Domain.PostLikes;
using Choco.Domain.Posts;
using Choco.Domain.Users;

namespace Choco.Application.Posts;

internal sealed class PostResponseReader(
    IPostRepository postRepository,
    IUserRepository userRepository,
    IPostLikeRepository likeRepository)
{
    public async Task<PageResponse<PostResponse>> ReadByAuthorAsync(
        Guid authorId, string? cursor, int limit, Guid viewerId, CancellationToken cancellationToken)
    {
        IReadOnlyList<Post> posts = await postRepository.ListByAuthorForReadAsync(
            authorId, PostPages.ReadPosition(cursor), limit + 1, cancellationToken);

        return await ToPageAsync(posts, limit, viewerId, cancellationToken);
    }

    public async Task<PageResponse<PostResponse>> ReadByFollowedAuthorsAsync(
        Guid followerId, string? cursor, int limit, CancellationToken cancellationToken)
    {
        IReadOnlyList<Post> posts = await postRepository.ListByFollowedAuthorsForReadAsync(
            followerId, PostPages.ReadPosition(cursor), limit + 1, cancellationToken);

        return await ToPageAsync(posts, limit, followerId, cancellationToken);
    }

    public async Task<PageResponse<PostResponse>> ReadRecommendedAsync(
        Guid viewerId, string? cursor, int limit, CancellationToken cancellationToken)
    {
        IReadOnlyList<Post> posts = await postRepository.ListRecommendedForReadAsync(
            viewerId, PostPages.ReadPosition(cursor), limit + 1, cancellationToken);

        return await ToPageAsync(posts, limit, viewerId, cancellationToken);
    }

    public async Task<PostResponse> ReadOneAsync(
        Post post, User author, Guid viewerId, CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<Guid, PostLikeSummary> likes = await likeRepository.GetSummariesForReadAsync(
            [post.Id], viewerId, cancellationToken);

        return post.ToResponse(author, likes.GetValueOrDefault(post.Id, PostLikeSummary.None));
    }

    private async Task<PageResponse<PostResponse>> ToPageAsync(
        IReadOnlyList<Post> posts, int limit, Guid viewerId, CancellationToken cancellationToken)
    {
        List<Post> page = posts.Take(limit).ToList();
        if (page.Count == 0)
        {
            return PostPages.ToPage(posts, [], new Dictionary<Guid, PostLikeSummary>(), limit);
        }

        Guid[] authorIds = page.Select(post => post.AuthorId).Distinct().ToArray();
        Guid[] postIds = page.Select(post => post.Id).ToArray();
        IReadOnlyList<User> authors = await userRepository.ListByIdsForReadAsync(authorIds, cancellationToken);
        IReadOnlyDictionary<Guid, PostLikeSummary> likes = await likeRepository.GetSummariesForReadAsync(
            postIds, viewerId, cancellationToken);

        return PostPages.ToPage(posts, authors, likes, limit);
    }
}
