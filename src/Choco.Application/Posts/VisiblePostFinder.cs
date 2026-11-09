using Choco.Domain.Posts;
using Choco.Domain.Users;

namespace Choco.Application.Posts;

internal sealed class VisiblePostFinder(IPostRepository postRepository, IUserRepository userRepository)
{
    public async Task<Post?> FindAsync(Guid postId, CancellationToken cancellationToken)
    {
        Post? post = await postRepository.GetByIdForReadAsync(postId, cancellationToken);
        if (post is null)
        {
            return null;
        }

        User? author = await userRepository.GetByIdForReadAsync(post.AuthorId, cancellationToken);

        return author is null ? null : post;
    }
}
