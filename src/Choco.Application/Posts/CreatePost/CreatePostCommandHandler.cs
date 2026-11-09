using Choco.Application.Abstractions.Authentication;
using Choco.Application.Abstractions.Messaging;
using Choco.Contract.Posts;
using Choco.Domain.PostLikes;
using Choco.Domain.Posts;
using Choco.Domain.Primitives.Persistence;
using Choco.Domain.Users;
using Choco.SharedKernel.Results;

namespace Choco.Application.Posts.CreatePost;

internal sealed class CreatePostCommandHandler : ICommandHandler<CreatePostCommand, PostResponse>
{
    private readonly ICurrentUser _currentUser;
    private readonly IPostRepository _postRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreatePostCommandHandler(
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

    public async Task<Result<PostResponse>> Handle(CreatePostCommand command, CancellationToken cancellationToken)
    {
        Result<Guid> authorIdResult = await _currentUser.GetIdAsync(cancellationToken);

        if (authorIdResult.IsFailure)
        {
            return Result<PostResponse>.Failure(authorIdResult.Error);
        }

        User? author = await _userRepository.GetByIdForReadAsync(authorIdResult.Value, cancellationToken);

        if (author is null)
        {
            return Result<PostResponse>.Failure(UserErrors.Deleted);
        }

        Post post = Post.Create(command.Content, author.Id);

        _postRepository.Add(post);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<PostResponse>.Success(post.ToResponse(author, PostLikeSummary.None));
    }
}
