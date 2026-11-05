using Choco.Application.Abstractions.Authentication;
using Choco.Application.Abstractions.Messaging;
using Choco.Contract.Abstractions;
using Choco.Contract.Posts;
using Choco.Domain.Posts;
using Choco.SharedKernel.Results;

namespace Choco.Application.Posts.GetMyPosts;

internal sealed class GetMyPostsQueryHandler : IQueryHandler<GetMyPostsQuery, PageResponse<PostResponse>>
{
    private readonly ICurrentUser _currentUser;
    private readonly IPostRepository _postRepository;

    public GetMyPostsQueryHandler(ICurrentUser currentUser, IPostRepository postRepository)
    {
        _currentUser = currentUser;
        _postRepository = postRepository;
    }

    public async Task<Result<PageResponse<PostResponse>>> Handle(GetMyPostsQuery query, CancellationToken cancellationToken)
    {
        Result<Guid> userIdResult = await _currentUser.GetIdAsync(cancellationToken);

        if (userIdResult.IsFailure)
        {
            return Result<PageResponse<PostResponse>>.Failure(userIdResult.Error);
        }

        (DateTimeOffset CreatedAt, Guid Id)? after = PostCursor.TryDecode(query.Cursor, out PostCursor? cursor)
            ? (cursor!.CreatedAt, cursor.Id)
            : null;

        IReadOnlyList<Post> posts = await _postRepository.ListByAuthorForReadAsync(
            userIdResult.Value, after, query.Limit + 1, cancellationToken);

        List<Post> page = posts.Take(query.Limit).ToList();
        string? nextCursor = posts.Count > query.Limit
            ? new PostCursor(page[^1].CreatedAt, page[^1].Id).Encode()
            : null;

        return Result<PageResponse<PostResponse>>.Success(
            new PageResponse<PostResponse>(page.Select(post => post.ToResponse()).ToList(), nextCursor));
    }
}
