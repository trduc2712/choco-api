using Choco.Application.Abstractions.Authentication;
using Choco.Application.Abstractions.Messaging;
using Choco.Application.Posts;
using Choco.Contract.Abstractions;
using Choco.Contract.Comments;
using Choco.Domain.Comments;
using Choco.Domain.Posts;
using Choco.Domain.UserBlocks;
using Choco.Domain.Users;
using Choco.SharedKernel.Results;

namespace Choco.Application.Comments.GetPostComments;

internal sealed class GetPostCommentsQueryHandler(
    ICurrentUser currentUser,
    VisiblePostFinder postFinder,
    ICommentRepository commentRepository,
    IUserRepository userRepository,
    IUserBlockRepository blockRepository) : IQueryHandler<GetPostCommentsQuery, PageResponse<CommentResponse>>
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

        IReadOnlyList<Comment> comments = await commentRepository.ListByPostForReadAsync(
            post.Id, caller.Value, PostPages.ReadPosition(query.Cursor), query.Limit + 1, cancellationToken);

        Guid[] authorIds = comments.Take(query.Limit).Select(comment => comment.AuthorId).Distinct().ToArray();
        IReadOnlyList<User> authors = authorIds.Length == 0
            ? []
            : await userRepository.ListByIdsForReadAsync(authorIds, cancellationToken);

        return Result<PageResponse<CommentResponse>>.Success(CommentPages.ToPage(comments, authors, query.Limit));
    }
}
