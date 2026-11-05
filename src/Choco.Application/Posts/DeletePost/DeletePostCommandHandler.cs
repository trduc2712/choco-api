using Choco.Application.Abstractions.Authentication;
using Choco.Application.Abstractions.Authorization;
using Choco.Application.Abstractions.Messaging;
using Choco.Contract.Posts;
using Choco.Domain.Posts;
using Choco.Domain.Primitives.Persistence;
using Choco.Domain.Roles;
using Choco.SharedKernel.Results;

namespace Choco.Application.Posts.DeletePost;

internal sealed class DeletePostCommandHandler : ICommandHandler<DeletePostCommand>
{
    private readonly ICurrentUser _currentUser;
    private readonly IPermissionChecker _permissionChecker;
    private readonly IPostRepository _postRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DeletePostCommandHandler(
        ICurrentUser currentUser,
        IPermissionChecker permissionChecker,
        IPostRepository postRepository,
        IUnitOfWork unitOfWork)
    {
        _currentUser = currentUser;
        _permissionChecker = permissionChecker;
        _postRepository = postRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(DeletePostCommand command, CancellationToken cancellationToken)
    {
        Result<Guid> userIdResult = await _currentUser.GetIdAsync(cancellationToken);

        if (userIdResult.IsFailure)
        {
            return userIdResult;
        }

        Post? post = await _postRepository.GetByIdAsync(command.Id, cancellationToken);

        if (post is null)
        {
            return Result.Failure(PostErrors.NotFound);
        }

        bool canDeleteAnyPost = false;

        if (post.AuthorId != userIdResult.Value)
        {
            Result<bool> isGranted = await _permissionChecker.IsGrantedToCurrentUserAsync(
                Permission.DeleteAnyPost, PermissionScope.Platform, cancellationToken);

            if (isGranted.IsFailure)
            {
                return isGranted;
            }

            canDeleteAnyPost = isGranted.Value;
        }

        Result deleteCheck = post.CheckCanDelete(userIdResult.Value, canDeleteAnyPost);

        if (deleteCheck.IsFailure)
        {
            return deleteCheck;
        }

        _postRepository.Remove(post);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
