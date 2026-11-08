using Choco.Application.Abstractions.Authentication;
using Choco.Application.Abstractions.Messaging;
using Choco.Contract.Abstractions;
using Choco.Contract.Posts;
using Choco.Domain.Posts;
using Choco.SharedKernel.Results;

namespace Choco.Application.Posts.GetRecommendedFeed;

internal sealed class GetRecommendedFeedQueryHandler(ICurrentUser currentUser, IPostRepository postRepository)
    : IQueryHandler<GetRecommendedFeedQuery, PageResponse<PostResponse>>
{
    public async Task<Result<PageResponse<PostResponse>>> Handle(
        GetRecommendedFeedQuery query, CancellationToken cancellationToken)
    {
        Result<Guid> caller = await currentUser.GetIdAsync(cancellationToken);
        if (caller.IsFailure)
        {
            return Result<PageResponse<PostResponse>>.Failure(caller.Error);
        }

        PageResponse<PostResponse> page = await PostPages.ReadRecommendedAsync(
            postRepository, caller.Value, query.Cursor, query.Limit, cancellationToken);

        return Result<PageResponse<PostResponse>>.Success(page);
    }
}
