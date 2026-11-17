using Choco.Application.Posts;
using Choco.Contract.Abstractions;
using Choco.Contract.Comments;
using Choco.Domain.CommentReactions;
using Choco.Domain.Comments;
using Choco.Domain.Users;

namespace Choco.Application.Comments;

internal sealed class CommentPageReader(
    ICommentRepository commentRepository,
    IUserRepository userRepository,
    ICommentReactionRepository reactionRepository)
{
    public async Task<PageResponse<CommentResponse>> ReadAsync(
        Guid postId, Guid? parentId, Guid viewerId, string? cursor, int limit, CancellationToken cancellationToken)
    {
        IReadOnlyList<CommentThreadEntry> entries = await commentRepository.ListThreadLevelForReadAsync(
            postId, parentId, viewerId, PostPages.ReadPosition(cursor), limit + 1, cancellationToken);

        CommentThreadEntry[] visible = entries.Take(limit).Where(entry => entry.IsVisible).ToArray();

        Guid[] authorIds = visible.Select(entry => entry.Comment.AuthorId).Distinct().ToArray();
        IReadOnlyList<User> authors = authorIds.Length == 0
            ? []
            : await userRepository.ListByIdsForReadAsync(authorIds, cancellationToken);

        Guid[] commentIds = visible.Select(entry => entry.Comment.Id).ToArray();
        IReadOnlyDictionary<Guid, CommentReactionSummary> reactions = commentIds.Length == 0
            ? new Dictionary<Guid, CommentReactionSummary>()
            : await reactionRepository.GetSummariesForReadAsync(commentIds, viewerId, cancellationToken);

        return CommentPages.ToPage(entries, authors, reactions, limit);
    }
}
