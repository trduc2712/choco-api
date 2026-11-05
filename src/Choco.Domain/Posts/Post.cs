using Choco.Domain.Primitives.Entities;
using Choco.SharedKernel.Results;

namespace Choco.Domain.Posts;

public sealed class Post : AggregateRoot, IAuditable, ISoftDeletable
{
    public const int ContentMaxLength = 500;

    public string Content { get; private set; } = null!;
    public Guid AuthorId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ModifiedAt { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }

    private Post() { }

    private Post(Guid id, string content, Guid authorId)
        : base(id)
    {
        Content = content;
        AuthorId = authorId;
    }

    public static Post Create(string content, Guid authorId)
    {
        string normalizedContent = NormalizeContent(content);

        if (authorId == Guid.Empty)
        {
            throw new ArgumentException("Value cannot be empty.", nameof(authorId));
        }

        return new Post(
            Guid.NewGuid(),
            normalizedContent,
            authorId);
    }

    public Result Edit(string content, Guid editorId)
    {
        string normalizedContent = NormalizeContent(content);

        if (editorId != AuthorId)
        {
            return Result.Failure(PostErrors.NotAuthor);
        }

        Content = normalizedContent;

        return Result.Success();
    }

    private static string NormalizeContent(string content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        string trimmedContent = content.Trim();
        ArgumentOutOfRangeException.ThrowIfGreaterThan(trimmedContent.Length, ContentMaxLength, nameof(content));

        return trimmedContent;
    }
}
