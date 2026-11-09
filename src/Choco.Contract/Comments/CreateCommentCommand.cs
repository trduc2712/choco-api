using Choco.Contract.Abstractions.Messaging;

namespace Choco.Contract.Comments;

/// <summary>
/// A comment to add to a post.
/// </summary>
/// <param name="PostId">Ignored; the post is the one named in the route.</param>
/// <param name="Content">The comment text. Required, at most 500 characters once leading and trailing whitespace is removed.</param>
/// <example>{ "content": "content" }</example>
public sealed record CreateCommentCommand(Guid PostId, string Content) : ICommand<CommentResponse>;
