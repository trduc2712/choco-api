using Choco.Contract.Abstractions.Messaging;

namespace Choco.Contract.Comments;

/// <summary>
/// A reply to a comment.
/// </summary>
/// <param name="CommentId">Ignored; the comment is the one named in the route.</param>
/// <param name="Content">The reply text. Required, at most 500 characters once leading and trailing whitespace is removed.</param>
/// <example>{ "content": "content" }</example>
public sealed record ReplyToCommentCommand(Guid CommentId, string Content) : ICommand<CommentResponse>;
