using Choco.Contract.Abstractions.Messaging;

namespace Choco.Contract.Comments;

/// <summary>
/// The caller's reaction to a comment.
/// </summary>
/// <param name="CommentId">Ignored; the comment is the one named in the route.</param>
/// <param name="Type">The reaction: like or dislike.</param>
/// <example>{ "type": "like" }</example>
public sealed record SetCommentReactionCommand(Guid CommentId, string Type) : ICommand;
