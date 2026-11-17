using Choco.Contract.Abstractions.Messaging;

namespace Choco.Contract.Comments;

/// <summary>Removes the caller's reaction from a comment.</summary>
/// <param name="CommentId">The comment's id.</param>
/// <example>{"commentId":"00000000-0000-0000-0000-000000000001"}</example>
public sealed record RemoveCommentReactionCommand(Guid CommentId) : ICommand;
