using Choco.Application.Abstractions.Authentication;
using Choco.Application.Abstractions.Messaging;
using Choco.Application.Posts;
using Choco.Contract.Abstractions;
using Choco.Contract.Comments;
using Choco.Domain.Comments;
using Choco.Domain.Posts;
using Choco.Domain.UserBlocks;
using Choco.SharedKernel.Results;

namespace Choco.Application.Comments.GetCommentReplies;

internal sealed class GetCommentRepliesQueryHandler(
    ICurrentUser currentUser,
    ICommentRepository commentRepository,
    VisiblePostFinder postFinder,
    IUserBlockRepository blockRepository,
    CommentPageReader pageReader) : IQueryHandler<GetCommentRepliesQuery, PageResponse<CommentResponse>>
{
    public async Task<Result<PageResponse<CommentResponse>>> Handle(
        GetCommentRepliesQuery query, CancellationToken cancellationToken)
    {
        Result<Guid> caller = await currentUser.GetIdAsync(cancellationToken);
        if (caller.IsFailure)
        {
            return Result<PageResponse<CommentResponse>>.Failure(caller.Error);
        }

        Comment? comment = await commentRepository.GetByIdIncludingDeletedForReadAsync(query.CommentId, cancellationToken);
        if (comment is null)
        {
            return Result<PageResponse<CommentResponse>>.Failure(CommentErrors.NotFound);
        }

        Post? post = await postFinder.FindAsync(comment.PostId, cancellationToken);
        if (post is null || await blockRepository.ExistsBetweenForReadAsync(caller.Value, post.AuthorId, cancellationToken))
        {
            return Result<PageResponse<CommentResponse>>.Failure(CommentErrors.NotFound);
        }

        PageResponse<CommentResponse> page = await pageReader.ReadAsync(
            post.Id, comment.Id, caller.Value, query.Cursor, query.Limit, cancellationToken);

        return Result<PageResponse<CommentResponse>>.Success(page);
    }
}
