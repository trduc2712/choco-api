using Choco.Application.Abstractions.Authentication;
using Choco.Application.Abstractions.Messaging;
using Choco.Application.Posts;
using Choco.Contract.Comments;
using Choco.Domain.Comments;
using Choco.Domain.Posts;
using Choco.Domain.Primitives.Persistence;
using Choco.Domain.UserBlocks;
using Choco.Domain.Users;
using Choco.SharedKernel.Results;

namespace Choco.Application.Comments.ReplyToComment;

internal sealed class ReplyToCommentCommandHandler(
    ICurrentUser currentUser,
    ICommentRepository commentRepository,
    VisiblePostFinder postFinder,
    IUserRepository userRepository,
    IUserBlockRepository blockRepository,
    IUnitOfWork unitOfWork) : ICommandHandler<ReplyToCommentCommand, CommentResponse>
{
    public async Task<Result<CommentResponse>> Handle(ReplyToCommentCommand command, CancellationToken cancellationToken)
    {
        Result<Guid> caller = await currentUser.GetIdAsync(cancellationToken);
        if (caller.IsFailure)
        {
            return Result<CommentResponse>.Failure(caller.Error);
        }

        Comment? parent = await commentRepository.GetByIdForReadAsync(command.CommentId, cancellationToken);
        if (parent is null || await userRepository.GetByIdForReadAsync(parent.AuthorId, cancellationToken) is null)
        {
            return Result<CommentResponse>.Failure(CommentErrors.NotFound);
        }

        Post? post = await postFinder.FindAsync(parent.PostId, cancellationToken);
        if (post is null)
        {
            return Result<CommentResponse>.Failure(CommentErrors.NotFound);
        }

        if (await blockRepository.ExistsBetweenForReadAsync(caller.Value, post.AuthorId, cancellationToken)
            || await blockRepository.ExistsBetweenForReadAsync(caller.Value, parent.AuthorId, cancellationToken))
        {
            return Result<CommentResponse>.Failure(UserBlockErrors.Blocked);
        }

        User? author = await userRepository.GetByIdForReadAsync(caller.Value, cancellationToken);
        if (author is null)
        {
            return Result<CommentResponse>.Failure(UserErrors.Deleted);
        }

        Result<Comment> reply = parent.Reply(author.Id, command.Content);
        if (reply.IsFailure)
        {
            return Result<CommentResponse>.Failure(reply.Error);
        }

        commentRepository.Add(reply.Value);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<CommentResponse>.Success(reply.Value.ToResponse(author));
    }
}
