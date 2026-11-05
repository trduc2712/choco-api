using Choco.Application.Abstractions.Messaging;
using Choco.Contract.Posts;
using Choco.Domain.Posts;
using Choco.SharedKernel.Results;

namespace Choco.Application.Posts.GetPost;

internal sealed class GetPostQueryHandler : IQueryHandler<GetPostQuery, PostResponse>
{
    private readonly IPostRepository _postRepository;

    public GetPostQueryHandler(IPostRepository postRepository)
    {
        _postRepository = postRepository;
    }

    public async Task<Result<PostResponse>> Handle(GetPostQuery query, CancellationToken cancellationToken)
    {
        Post? post = await _postRepository.GetByIdForReadAsync(query.Id, cancellationToken);

        return post is null
            ? Result<PostResponse>.Failure(PostErrors.NotFound)
            : Result<PostResponse>.Success(post.ToResponse());
    }
}
