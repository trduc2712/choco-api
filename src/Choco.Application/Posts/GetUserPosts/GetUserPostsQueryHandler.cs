using Choco.Application.Abstractions.Authentication;
using Choco.Application.Abstractions.Messaging;
using Choco.Contract.Abstractions;
using Choco.Contract.Posts;
using Choco.Domain.Posts;
using Choco.Domain.Users;
using Choco.SharedKernel.Results;

namespace Choco.Application.Posts.GetUserPosts;

internal sealed class GetUserPostsQueryHandler(
    ICurrentUser currentUser,
    IUserRepository userRepository,
    IPostRepository postRepository)
    : IQueryHandler<GetUserPostsQuery, PageResponse<PostResponse>>
{
    public async Task<Result<PageResponse<PostResponse>>> Handle(GetUserPostsQuery query, CancellationToken cancellationToken)
    {
        Result<Guid> caller = await currentUser.GetIdAsync(cancellationToken);
        if (caller.IsFailure)
        {
            return Result<PageResponse<PostResponse>>.Failure(caller.Error);
        }

        User? author = await userRepository.GetByIdForReadAsync(query.UserId, cancellationToken);
        if (author is null)
        {
            return Result<PageResponse<PostResponse>>.Failure(UserErrors.NotFound);
        }

        PageResponse<PostResponse> page = await PostPages.ReadByAuthorAsync(
            postRepository, author.Id, query.Cursor, query.Limit, cancellationToken);

        return Result<PageResponse<PostResponse>>.Success(page);
    }
}
