using Choco.Application.Abstractions.Authentication;
using Choco.Application.Abstractions.Messaging;
using Choco.Contract.Abstractions;
using Choco.Contract.Posts;
using Choco.SharedKernel.Results;

namespace Choco.Application.Posts.GetMyPosts;

internal sealed class GetMyPostsQueryHandler : IQueryHandler<GetMyPostsQuery, PageResponse<PostResponse>>
{
    private readonly ICurrentUser _currentUser;
    private readonly PostResponseReader _reader;

    public GetMyPostsQueryHandler(ICurrentUser currentUser, PostResponseReader reader)
    {
        _currentUser = currentUser;
        _reader = reader;
    }

    public async Task<Result<PageResponse<PostResponse>>> Handle(GetMyPostsQuery query, CancellationToken cancellationToken)
    {
        Result<Guid> userIdResult = await _currentUser.GetIdAsync(cancellationToken);

        if (userIdResult.IsFailure)
        {
            return Result<PageResponse<PostResponse>>.Failure(userIdResult.Error);
        }

        PageResponse<PostResponse> page = await _reader.ReadByAuthorAsync(
            userIdResult.Value, query.Cursor, query.Limit, userIdResult.Value, cancellationToken);

        return Result<PageResponse<PostResponse>>.Success(page);
    }
}
