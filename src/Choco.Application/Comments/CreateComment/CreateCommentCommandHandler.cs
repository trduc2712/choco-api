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

namespace Choco.Application.Comments.CreateComment;

internal sealed class CreateCommentCommandHandler(
    ICurrentUser currentUser,
    VisiblePostFinder postFinder,
    IUserRepository userRepository,
    IUserBlockRepository blockRepository,
    ICommentRepository commentRepository,
    IUnitOfWork unitOfWork) : ICommandHandler<CreateCommentCommand, CommentResponse>
{
    public async Task<Result<CommentResponse>> Handle(CreateCommentCommand command, CancellationToken cancellationToken)
    {
        Result<Guid> caller = await currentUser.GetIdAsync(cancellationToken);
        if (caller.IsFailure)
        {
            return Result<CommentResponse>.Failure(caller.Error);
        }

        Post? post = await postFinder.FindAsync(command.PostId, cancellationToken);
        if (post is null)
        {
            return Result<CommentResponse>.Failure(PostErrors.NotFound);
        }

        if (await blockRepository.ExistsBetweenForReadAsync(caller.Value, post.AuthorId, cancellationToken))
        {
            return Result<CommentResponse>.Failure(UserBlockErrors.Blocked);
        }

        User? author = await userRepository.GetByIdForReadAsync(caller.Value, cancellationToken);
        if (author is null)
        {
            return Result<CommentResponse>.Failure(UserErrors.Deleted);
        }

        Comment comment = Comment.Create(post.Id, author.Id, command.Content);

        commentRepository.Add(comment);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<CommentResponse>.Success(comment.ToResponse(author));
    }
}
