using Choco.Application.Abstractions.Authentication;
using Choco.Application.Abstractions.Messaging;
using Choco.Contract.Posts;
using Choco.Domain.Posts;
using Choco.Domain.Users;
using Choco.SharedKernel.Results;

namespace Choco.Application.Posts.GetPost;

internal sealed class GetPostQueryHandler : IQueryHandler<GetPostQuery, PostResponse>
{
    private readonly ICurrentUser _currentUser;
    private readonly IPostRepository _postRepository;
    private readonly IUserRepository _userRepository;
    private readonly PostResponseReader _reader;

    public GetPostQueryHandler(
        ICurrentUser currentUser,
        IPostRepository postRepository,
        IUserRepository userRepository,
        PostResponseReader reader)
    {
        _currentUser = currentUser;
        _postRepository = postRepository;
        _userRepository = userRepository;
        _reader = reader;
    }

    public async Task<Result<PostResponse>> Handle(GetPostQuery query, CancellationToken cancellationToken)
    {
        Result<Guid> caller = await _currentUser.GetIdAsync(cancellationToken);

        if (caller.IsFailure)
        {
            return Result<PostResponse>.Failure(caller.Error);
        }

        Post? post = await _postRepository.GetByIdForReadAsync(query.Id, cancellationToken);

        if (post is null)
        {
            return Result<PostResponse>.Failure(PostErrors.NotFound);
        }

        User? author = await _userRepository.GetByIdForReadAsync(post.AuthorId, cancellationToken);

        return author is null
            ? Result<PostResponse>.Failure(PostErrors.NotFound)
            : Result<PostResponse>.Success(await _reader.ReadOneAsync(post, author, caller.Value, cancellationToken));
    }
}
