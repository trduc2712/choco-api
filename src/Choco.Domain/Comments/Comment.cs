using Choco.Domain.Primitives.Entities;
using Choco.SharedKernel.Results;

namespace Choco.Domain.Comments;

public sealed class Comment : AggregateRoot, IAuditable, ISoftDeletable
{
    public const int ContentMaxLength = 500;
    public const int MaxDepth = 5;

    public Guid PostId { get; private set; }
    public Guid AuthorId { get; private set; }
    public string Content { get; private set; } = null!;
    public Guid? ParentId { get; private set; }
    public Guid? RootId { get; private set; }
    public int Depth { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ModifiedAt { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }

    private Comment() { }

    private Comment(Guid id, Guid postId, Guid authorId, string content, Guid? parentId, Guid? rootId, int depth)
        : base(id)
    {
        PostId = postId;
        AuthorId = authorId;
        Content = content;
        ParentId = parentId;
        RootId = rootId;
        Depth = depth;
    }

    public static Comment Create(Guid postId, Guid authorId, string content)
    {
        if (postId == Guid.Empty)
        {
            throw new ArgumentException("Value cannot be empty.", nameof(postId));
        }

        Comment comment = new(Guid.NewGuid(), postId, GuardAuthorId(authorId), TrimContent(content), null, null, 0);
        comment.RaiseDomainEvent(new CommentCreated(comment.Id, postId, authorId, null));

        return comment;
    }

    public Result<Comment> Reply(Guid authorId, string content)
    {
        Guid guardedAuthorId = GuardAuthorId(authorId);
        string trimmedContent = TrimContent(content);

        if (Depth >= MaxDepth)
        {
            return Result<Comment>.Failure(CommentErrors.MaxDepthReached);
        }

        Comment reply = new(Guid.NewGuid(), PostId, guardedAuthorId, trimmedContent, Id, RootId ?? Id, Depth + 1);
        reply.RaiseDomainEvent(new CommentCreated(reply.Id, PostId, guardedAuthorId, Id));

        return Result<Comment>.Success(reply);
    }

    private static Guid GuardAuthorId(Guid authorId)
    {
        if (authorId == Guid.Empty)
        {
            throw new ArgumentException("Value cannot be empty.", nameof(authorId));
        }

        return authorId;
    }

    private static string TrimContent(string content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        string trimmedContent = content.Trim();
        ArgumentOutOfRangeException.ThrowIfGreaterThan(trimmedContent.Length, ContentMaxLength, nameof(content));

        return trimmedContent;
    }

    public Result CheckCanDelete(Guid userId, bool canDeleteAnyComment)
    {
        return userId == AuthorId || canDeleteAnyComment
            ? Result.Success()
            : Result.Failure(CommentErrors.NotAuthor);
    }
}
