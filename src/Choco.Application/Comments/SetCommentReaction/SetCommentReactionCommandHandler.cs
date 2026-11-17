using Choco.Application.Abstractions.Authentication;
using Choco.Application.Abstractions.Messaging;
using Choco.Application.Posts;
using Choco.Contract.Comments;
using Choco.Domain.CommentReactions;
using Choco.Domain.Comments;
using Choco.Domain.Posts;
using Choco.Domain.Primitives.Persistence;
using Choco.Domain.UserBlocks;
using Choco.Domain.Users;
using Choco.SharedKernel.Results;

namespace Choco.Application.Comments.SetCommentReaction;

internal sealed class SetCommentReactionCommandHandler(
    ICurrentUser currentUser,
    ICommentRepository commentRepository,
    VisiblePostFinder postFinder,
    IUserRepository userRepository,
    IUserBlockRepository blockRepository,
    ICommentReactionRepository reactionRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<SetCommentReactionCommand>
{
    public async Task<Result> Handle(SetCommentReactionCommand command, CancellationToken cancellationToken)
    {
        Result<Guid> caller = await currentUser.GetIdAsync(cancellationToken);
        if (caller.IsFailure)
        {
            return caller;
        }

        Comment? comment = await commentRepository.GetByIdForReadAsync(command.CommentId, cancellationToken);
        if (comment is null || await userRepository.GetByIdForReadAsync(comment.AuthorId, cancellationToken) is null)
        {
            return Result.Failure(CommentErrors.NotFound);
        }

        Post? post = await postFinder.FindAsync(comment.PostId, cancellationToken);
        if (post is null)
        {
            return Result.Failure(CommentErrors.NotFound);
        }

        if (await blockRepository.ExistsBetweenForReadAsync(caller.Value, post.AuthorId, cancellationToken)
            || await blockRepository.ExistsBetweenForReadAsync(caller.Value, comment.AuthorId, cancellationToken))
        {
            return Result.Failure(UserBlockErrors.Blocked);
        }

        CommentReactionTypes.TryParse(command.Type, out CommentReactionType type);
        DateTimeOffset now = timeProvider.GetUtcNow();

        await unitOfWork.InTransactionAsync(async () =>
        {
            CommentReaction reaction = CommentReaction.Create(comment.Id, caller.Value, type, now);

            if (!await reactionRepository.AddIfMissingAsync(reaction, cancellationToken))
            {
                CommentReaction? existing = await reactionRepository.GetAsync(comment.Id, caller.Value, cancellationToken);
                if (existing is null)
                {
                    reactionRepository.Add(reaction);
                }
                else
                {
                    existing.ChangeTo(type, now);
                }
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);

            return true;
        }, cancellationToken);

        return Result.Success();
    }
}
