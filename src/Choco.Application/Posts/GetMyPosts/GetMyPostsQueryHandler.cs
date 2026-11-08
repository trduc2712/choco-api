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

        PageResponse<PostResponse> page = await PostPages.ReadByAuthorAsync(
            _postRepository, userIdResult.Value, query.Cursor, query.Limit, cancellationToken);

        return Result<PageResponse<PostResponse>>.Success(page);
    }
}
