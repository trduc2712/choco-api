using Choco.Application.Abstractions.Authentication;
using Choco.Application.Abstractions.Messaging;
using Choco.Contract.Posts;
using Choco.Domain.Posts;
using Choco.Domain.Primitives.Persistence;
using Choco.Domain.Users;
using Choco.SharedKernel.Results;

namespace Choco.Application.Posts.EditPost;

internal sealed class EditPostCommandHandler : ICommandHandler<EditPostCommand, PostResponse>
{
    private readonly ICurrentUser _currentUser;
    private readonly IPostRepository _postRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;

    public EditPostCommandHandler(
        ICurrentUser currentUser,
        IPostRepository postRepository,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork)
    {
        _currentUser = currentUser;
        _postRepository = postRepository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<PostResponse>> Handle(EditPostCommand command, CancellationToken cancellationToken)
    {
        Result<Guid> editorIdResult = await _currentUser.GetIdAsync(cancellationToken);

        if (editorIdResult.IsFailure)
        {
            return Result<PostResponse>.Failure(editorIdResult.Error);
        }

        Post? post = await _postRepository.GetByIdAsync(command.Id, cancellationToken);

        if (post is null)
        {
            return Result<PostResponse>.Failure(PostErrors.NotFound);
        }

        User? author = await _userRepository.GetByIdForReadAsync(post.AuthorId, cancellationToken);

        if (author is null)
        {
            return Result<PostResponse>.Failure(PostErrors.NotFound);
        }

        Result editResult = post.Edit(command.Content, editorIdResult.Value);

        if (editResult.IsFailure)
        {
            return Result<PostResponse>.Failure(editResult.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<PostResponse>.Success(post.ToResponse(author));
    }
}
