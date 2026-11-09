using Choco.Application.Abstractions.Authentication;
using Choco.Application.Abstractions.Messaging;
using Choco.Contract.Abstractions;
using Choco.Contract.Posts;
using Choco.SharedKernel.Results;

namespace Choco.Application.Posts.GetFollowingFeed;

internal sealed class GetFollowingFeedQueryHandler(ICurrentUser currentUser, PostResponseReader reader)
    : IQueryHandler<GetFollowingFeedQuery, PageResponse<PostResponse>>
{
    public async Task<Result<PageResponse<PostResponse>>> Handle(
        GetFollowingFeedQuery query, CancellationToken cancellationToken)
    {
        Result<Guid> caller = await currentUser.GetIdAsync(cancellationToken);
        if (caller.IsFailure)
        {
            return Result<PageResponse<PostResponse>>.Failure(caller.Error);
        }

        PageResponse<PostResponse> page = await reader.ReadByFollowedAuthorsAsync(
            caller.Value, query.Cursor, query.Limit, cancellationToken);

        return Result<PageResponse<PostResponse>>.Success(page);
    }
}
