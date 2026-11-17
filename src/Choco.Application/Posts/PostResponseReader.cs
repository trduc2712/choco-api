using Choco.Contract.Abstractions;
using Choco.Contract.Posts;
using Choco.Domain.Comments;
using Choco.Domain.PostLikes;
using Choco.Domain.Posts;
using Choco.Domain.Users;

namespace Choco.Application.Posts;

internal sealed class PostResponseReader(
    IPostRepository postRepository,
    IUserRepository userRepository,
    IPostLikeRepository likeRepository,
    ICommentRepository commentRepository)
{
    public async Task<PageResponse<PostResponse>> ReadByAuthorAsync(
        Guid authorId, string? cursor, int limit, Guid viewerId, CancellationToken cancellationToken)
    {
        IReadOnlyList<Post> posts = await postRepository.ListByAuthorForReadAsync(
            authorId, viewerId, PostPages.ReadPosition(cursor), limit + 1, cancellationToken);

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
        Guid[] postIds = [post.Id];
        IReadOnlyDictionary<Guid, PostLikeSummary> likes = await likeRepository.GetSummariesForReadAsync(
            postIds, viewerId, cancellationToken);
        IReadOnlyDictionary<Guid, int> commentCounts = await commentRepository.CountByPostsForReadAsync(
            postIds, cancellationToken);
        IReadOnlyDictionary<Guid, Comment> topComments = await commentRepository.GetTopRootsByPostsForReadAsync(
            postIds, viewerId, cancellationToken);
        Comment? topComment = topComments.GetValueOrDefault(post.Id);
        IReadOnlyList<User> commenters = topComment is null
            ? []
            : await userRepository.ListByIdsForReadAsync([topComment.AuthorId], cancellationToken);

        return post.ToResponse(
            author,
            likes.GetValueOrDefault(post.Id, PostLikeSummary.None),
            commentCounts.GetValueOrDefault(post.Id),
            topComment.ToTopCommentResponse(commenters.ToDictionary(user => user.Id)));
    }

    private async Task<PageResponse<PostResponse>> ToPageAsync(
        IReadOnlyList<Post> posts, int limit, Guid viewerId, CancellationToken cancellationToken)
    {
        List<Post> page = posts.Take(limit).ToList();
        if (page.Count == 0)
        {
            return PostPages.ToPage(
                posts,
                [],
                new Dictionary<Guid, PostLikeSummary>(),
                new Dictionary<Guid, int>(),
                new Dictionary<Guid, Comment>(),
                limit);
        }

        Guid[] postIds = page.Select(post => post.Id).ToArray();
        IReadOnlyDictionary<Guid, Comment> topComments = await commentRepository.GetTopRootsByPostsForReadAsync(
            postIds, viewerId, cancellationToken);
        Guid[] userIds = page.Select(post => post.AuthorId)
            .Concat(topComments.Values.Select(comment => comment.AuthorId))
            .Distinct()
            .ToArray();
        IReadOnlyList<User> users = await userRepository.ListByIdsForReadAsync(userIds, cancellationToken);
        IReadOnlyDictionary<Guid, PostLikeSummary> likes = await likeRepository.GetSummariesForReadAsync(
            postIds, viewerId, cancellationToken);
        IReadOnlyDictionary<Guid, int> commentCounts = await commentRepository.CountByPostsForReadAsync(
            postIds, cancellationToken);

        return PostPages.ToPage(posts, users, likes, commentCounts, topComments, limit);
    }
}
