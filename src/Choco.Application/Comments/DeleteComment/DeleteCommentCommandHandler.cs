using Choco.Application.Abstractions.Authentication;
using Choco.Application.Abstractions.Authorization;
using Choco.Application.Abstractions.Messaging;
using Choco.Application.Posts;
using Choco.Contract.Comments;
using Choco.Domain.Comments;
using Choco.Domain.Posts;
using Choco.Domain.Primitives.Persistence;
using Choco.Domain.Roles;
using Choco.SharedKernel.Results;

namespace Choco.Application.Comments.DeleteComment;

internal sealed class DeleteCommentCommandHandler(
    ICurrentUser currentUser,
    IPermissionChecker permissionChecker,
    VisiblePostFinder postFinder,
    ICommentRepository commentRepository,
    IUnitOfWork unitOfWork) : ICommandHandler<DeleteCommentCommand>
{
    public async Task<Result> Handle(DeleteCommentCommand command, CancellationToken cancellationToken)
    {
        Result<Guid> caller = await currentUser.GetIdAsync(cancellationToken);
        if (caller.IsFailure)
        {
            return caller;
        }

        Comment? comment = await commentRepository.GetByIdAsync(command.CommentId, cancellationToken);
        if (comment is null)
        {
            return Result.Failure(CommentErrors.NotFound);
        }

        Post? post = await postFinder.FindAsync(comment.PostId, cancellationToken);
        if (post is null)
        {
            return Result.Failure(CommentErrors.NotFound);
        }

        bool canDeleteAnyComment = false;

        if (comment.AuthorId != caller.Value)
        {
            Result<bool> isGranted = await permissionChecker.IsGrantedToCurrentUserAsync(
                Permission.DeleteAnyComment, PermissionScope.Platform, cancellationToken);

            if (isGranted.IsFailure)
            {
                return isGranted;
            }

            canDeleteAnyComment = isGranted.Value;
        }

        Result deleteCheck = comment.CheckCanDelete(caller.Value, canDeleteAnyComment);
        if (deleteCheck.IsFailure)
        {
            return deleteCheck;
        }

        commentRepository.Remove(comment);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
