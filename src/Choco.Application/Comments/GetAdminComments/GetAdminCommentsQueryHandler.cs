using Choco.Application.Abstractions.Messaging;
using Choco.Application.Content;
using Choco.Contract.Abstractions;
using Choco.Contract.Comments;
using Choco.Domain.Comments;
using Choco.Domain.Primitives.Entities;
using Choco.Domain.Users;
using Choco.SharedKernel.Results;

namespace Choco.Application.Comments.GetAdminComments;

internal sealed class GetAdminCommentsQueryHandler(
    ICommentRepository commentRepository,
    IUserRepository userRepository) : IQueryHandler<GetAdminCommentsQuery, OffsetPageResponse<AdminCommentResponse>>
{
    public async Task<Result<OffsetPageResponse<AdminCommentResponse>>> Handle(
        GetAdminCommentsQuery query, CancellationToken cancellationToken)
    {
        ContentState state = ContentStates.TryParse(query.State, out ContentState parsedState)
            ? parsedState
            : ContentState.Visible;
        var filter = new CommentListFilter(
            SearchTerms.Split(query.Q, GetAdminCommentsQuery.MaxSearchTerms),
            query.AuthorId,
            query.PostId,
            state,
            query.From,
            query.To);
        long skip = (long)(query.Page - 1) * query.PageSize;

        int totalCount = await commentRepository.CountIncludingDeletedForReadAsync(filter, cancellationToken);
        IReadOnlyList<Comment> comments = skip >= totalCount
            ? []
            : await commentRepository.ListIncludingDeletedForReadAsync(filter, (int)skip, query.PageSize, cancellationToken);

        Dictionary<Guid, string> usernames = await ContentAuthors.LoadUsernamesAsync(
            userRepository, comments.Select(comment => comment.AuthorId), cancellationToken);

        List<AdminCommentResponse> items = comments
            .Select(comment => new AdminCommentResponse(
                comment.Id,
                comment.PostId,
                comment.Content,
                ContentAuthors.ToResponse(comment.AuthorId, usernames),
                comment.CreatedAt,
                comment.IsDeleted,
                comment.DeletedAt))
            .ToList();

        return Result<OffsetPageResponse<AdminCommentResponse>>.Success(
            new OffsetPageResponse<AdminCommentResponse>(items, query.Page, query.PageSize, totalCount));
    }
}
