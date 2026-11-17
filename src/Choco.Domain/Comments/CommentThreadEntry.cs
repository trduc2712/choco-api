namespace Choco.Domain.Comments;

public sealed record CommentThreadEntry(Comment Comment, bool IsVisible, int ReplyCount);
