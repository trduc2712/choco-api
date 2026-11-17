using Choco.Application.Abstractions.Authentication;
using Choco.Application.Abstractions.Messaging;
using Choco.Application.Posts;
using Choco.Contract.Abstractions;
using Choco.Contract.Comments;
using Choco.Domain.Posts;
using Choco.Domain.UserBlocks;
using Choco.SharedKernel.Results;

namespace Choco.Application.Comments.GetPostComments;

internal sealed class GetPostCommentsQueryHandler(
    ICurrentUser currentUser,
    VisiblePostFinder postFinder,
    IUserBlockRepository blockRepository,
    CommentPageReader pageReader) : IQueryHandler<GetPostCommentsQuery, PageResponse<CommentResponse>>
{
    public async Task<Result<PageResponse<CommentResponse>>> Handle(
        GetPostCommentsQuery query, CancellationToken cancellationToken)
    {
        Result<Guid> caller = await currentUser.GetIdAsync(cancellationToken);
        if (caller.IsFailure)
        {
            return Result<PageResponse<CommentResponse>>.Failure(caller.Error);
        }

        Post? post = await postFinder.FindAsync(query.PostId, cancellationToken);
        if (post is null || await blockRepository.ExistsBetweenForReadAsync(caller.Value, post.AuthorId, cancellationToken))
        {
            return Result<PageResponse<CommentResponse>>.Failure(PostErrors.NotFound);
        }

        PageResponse<CommentResponse> page = await pageReader.ReadAsync(
            post.Id, null, caller.Value, query.Cursor, query.Limit, cancellationToken);

        return Result<PageResponse<CommentResponse>>.Success(page);
    }
}
