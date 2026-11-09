using Choco.Domain.Primitives.Entities;
using Choco.SharedKernel.Results;

namespace Choco.Domain.Comments;

public sealed class Comment : AggregateRoot, IAuditable, ISoftDeletable
{
    public const int ContentMaxLength = 500;

    public Guid PostId { get; private set; }
    public Guid AuthorId { get; private set; }
    public string Content { get; private set; } = null!;

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ModifiedAt { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }

    private Comment() { }

    private Comment(Guid id, Guid postId, Guid authorId, string content)
        : base(id)
    {
        PostId = postId;
        AuthorId = authorId;
        Content = content;
    }

    public static Comment Create(Guid postId, Guid authorId, string content)
    {
        if (postId == Guid.Empty)
        {
            throw new ArgumentException("Value cannot be empty.", nameof(postId));
        }

        if (authorId == Guid.Empty)
        {
            throw new ArgumentException("Value cannot be empty.", nameof(authorId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        string trimmedContent = content.Trim();
        ArgumentOutOfRangeException.ThrowIfGreaterThan(trimmedContent.Length, ContentMaxLength, nameof(content));

        return new Comment(Guid.NewGuid(), postId, authorId, trimmedContent);
    }

    public Result CheckCanDelete(Guid userId, bool canDeleteAnyComment)
    {
        return userId == AuthorId || canDeleteAnyComment
            ? Result.Success()
            : Result.Failure(CommentErrors.NotAuthor);
    }
}
