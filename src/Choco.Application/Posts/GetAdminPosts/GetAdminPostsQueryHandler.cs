using Choco.Application.Abstractions.Messaging;
using Choco.Application.Content;
using Choco.Contract.Abstractions;
using Choco.Contract.Posts;
using Choco.Domain.Posts;
using Choco.Domain.Primitives.Entities;
using Choco.Domain.Users;
using Choco.SharedKernel.Results;

namespace Choco.Application.Posts.GetAdminPosts;

internal sealed class GetAdminPostsQueryHandler(
    IPostRepository postRepository,
    IUserRepository userRepository) : IQueryHandler<GetAdminPostsQuery, OffsetPageResponse<AdminPostResponse>>
{
    public async Task<Result<OffsetPageResponse<AdminPostResponse>>> Handle(
        GetAdminPostsQuery query, CancellationToken cancellationToken)
    {
        ContentState state = ContentStates.TryParse(query.State, out ContentState parsedState)
            ? parsedState
            : ContentState.Visible;
        var filter = new PostListFilter(
            SearchTerms.Split(query.Q, GetAdminPostsQuery.MaxSearchTerms), query.AuthorId, state, query.From, query.To);
        long skip = (long)(query.Page - 1) * query.PageSize;

        int totalCount = await postRepository.CountIncludingDeletedForReadAsync(filter, cancellationToken);
        IReadOnlyList<Post> posts = skip >= totalCount
            ? []
            : await postRepository.ListIncludingDeletedForReadAsync(filter, (int)skip, query.PageSize, cancellationToken);

        Dictionary<Guid, string> usernames = await ContentAuthors.LoadUsernamesAsync(
            userRepository, posts.Select(post => post.AuthorId), cancellationToken);

        List<AdminPostResponse> items = posts
            .Select(post => new AdminPostResponse(
                post.Id,
                post.Content,
                ContentAuthors.ToResponse(post.AuthorId, usernames),
                post.CreatedAt,
                post.IsDeleted,
                post.DeletedAt))
            .ToList();

        return Result<OffsetPageResponse<AdminPostResponse>>.Success(
            new OffsetPageResponse<AdminPostResponse>(items, query.Page, query.PageSize, totalCount));
    }
}
