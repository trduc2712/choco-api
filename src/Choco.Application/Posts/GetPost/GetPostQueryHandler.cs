using Choco.Application.Abstractions.Messaging;
using Choco.Contract.Posts;
using Choco.Domain.Posts;
using Choco.Domain.Users;
using Choco.SharedKernel.Results;

namespace Choco.Application.Posts.GetPost;

internal sealed class GetPostQueryHandler : IQueryHandler<GetPostQuery, PostResponse>
{
    private readonly IPostRepository _postRepository;
    private readonly IUserRepository _userRepository;

    public GetPostQueryHandler(IPostRepository postRepository, IUserRepository userRepository)
    {
        _postRepository = postRepository;
        _userRepository = userRepository;
    }

    public async Task<Result<PostResponse>> Handle(GetPostQuery query, CancellationToken cancellationToken)
    {
        Post? post = await _postRepository.GetByIdForReadAsync(query.Id, cancellationToken);

        if (post is null)
        {
            return Result<PostResponse>.Failure(PostErrors.NotFound);
        }

        User? author = await _userRepository.GetByIdForReadAsync(post.AuthorId, cancellationToken);

        return author is null
            ? Result<PostResponse>.Failure(PostErrors.NotFound)
            : Result<PostResponse>.Success(post.ToResponse(author));
    }
}
