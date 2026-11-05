using Choco.Application.Abstractions.Authentication;
using Choco.Application.Abstractions.Messaging;
using Choco.Contract.Posts;
using Choco.Domain.Posts;
using Choco.Domain.Primitives.Persistence;
using Choco.SharedKernel.Results;

namespace Choco.Application.Posts.CreatePost;

internal sealed class CreatePostCommandHandler : ICommandHandler<CreatePostCommand, PostResponse>
{
    private readonly ICurrentUser _currentUser;
    private readonly IPostRepository _postRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreatePostCommandHandler(ICurrentUser currentUser, IPostRepository postRepository, IUnitOfWork unitOfWork)
    {
        _currentUser = currentUser;
        _postRepository = postRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<PostResponse>> Handle(CreatePostCommand command, CancellationToken cancellationToken)
    {
        Result<Guid> authorIdResult = await _currentUser.GetIdAsync(cancellationToken);

        if (authorIdResult.IsFailure)
        {
            return Result<PostResponse>.Failure(authorIdResult.Error);
        }

        Post post = Post.Create(command.Content, authorIdResult.Value);

        _postRepository.Add(post);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<PostResponse>.Success(post.ToResponse());
    }
}
