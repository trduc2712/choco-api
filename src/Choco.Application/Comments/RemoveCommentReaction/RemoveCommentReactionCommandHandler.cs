using Choco.Application.Abstractions.Authentication;
using Choco.Application.Abstractions.Messaging;
using Choco.Application.Posts;
using Choco.Contract.Comments;
using Choco.Domain.CommentReactions;
using Choco.Domain.Comments;
using Choco.Domain.Users;
using Choco.SharedKernel.Results;

namespace Choco.Application.Comments.RemoveCommentReaction;

internal sealed class RemoveCommentReactionCommandHandler(
    ICurrentUser currentUser,
    ICommentRepository commentRepository,
    VisiblePostFinder postFinder,
    IUserRepository userRepository,
    ICommentReactionRepository reactionRepository) : ICommandHandler<RemoveCommentReactionCommand>
{
    public async Task<Result> Handle(RemoveCommentReactionCommand command, CancellationToken cancellationToken)
    {
        Result<Guid> caller = await currentUser.GetIdAsync(cancellationToken);
        if (caller.IsFailure)
        {
            return caller;
        }

        Comment? comment = await commentRepository.GetByIdForReadAsync(command.CommentId, cancellationToken);
        if (comment is null
            || await userRepository.GetByIdForReadAsync(comment.AuthorId, cancellationToken) is null
            || await postFinder.FindAsync(comment.PostId, cancellationToken) is null)
        {
            return Result.Failure(CommentErrors.NotFound);
        }

        await reactionRepository.RemoveIfPresentAsync(comment.Id, caller.Value, cancellationToken);

        return Result.Success();
    }
}
