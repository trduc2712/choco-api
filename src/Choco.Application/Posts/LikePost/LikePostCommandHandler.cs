using Choco.Application.Abstractions.Authentication;
using Choco.Application.Abstractions.Messaging;
using Choco.Contract.Posts;
using Choco.Domain.PostLikes;
using Choco.Domain.Posts;
using Choco.Domain.Primitives.Persistence;
using Choco.Domain.Users;
using Choco.SharedKernel.Results;

namespace Choco.Application.Posts.LikePost;

internal sealed class LikePostCommandHandler(
    ICurrentUser currentUser,
    IPostRepository postRepository,
    IUserRepository userRepository,
    IPostLikeRepository likeRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<LikePostCommand>
{
    public async Task<Result> Handle(LikePostCommand command, CancellationToken cancellationToken)
    {
        Result<Guid> caller = await currentUser.GetIdAsync(cancellationToken);
        if (caller.IsFailure)
        {
            return caller;
        }

        Post? post = await postRepository.GetByIdForReadAsync(command.PostId, cancellationToken);
        if (post is null || await userRepository.GetByIdForReadAsync(post.AuthorId, cancellationToken) is null)
        {
            return Result.Failure(PostErrors.NotFound);
        }

        PostLike like = PostLike.Create(post.Id, caller.Value, timeProvider.GetUtcNow());

        await unitOfWork.InTransactionAsync(async () =>
        {
            if (await likeRepository.AddIfMissingAsync(like, cancellationToken))
            {
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }

            return true;
        }, cancellationToken);

        return Result.Success();
    }
}
