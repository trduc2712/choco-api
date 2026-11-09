using Choco.Application.Abstractions.Authentication;
using Choco.Application.Abstractions.Messaging;
using Choco.Contract.Posts;
using Choco.Domain.PostLikes;
using Choco.Domain.Posts;
using Choco.Domain.Users;
using Choco.SharedKernel.Results;

namespace Choco.Application.Posts.UnlikePost;

internal sealed class UnlikePostCommandHandler(
    ICurrentUser currentUser,
    IPostRepository postRepository,
    IUserRepository userRepository,
    IPostLikeRepository likeRepository) : ICommandHandler<UnlikePostCommand>
{
    public async Task<Result> Handle(UnlikePostCommand command, CancellationToken cancellationToken)
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

        await likeRepository.RemoveIfPresentAsync(post.Id, caller.Value, cancellationToken);

        return Result.Success();
    }
}
