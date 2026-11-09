using Choco.Contract.Abstractions.Messaging;

namespace Choco.Contract.Comments;

/// <summary>Deletes a comment.</summary>
/// <param name="CommentId">The comment's id.</param>
/// <example>{"commentId":"00000000-0000-0000-0000-000000000001"}</example>
public sealed record DeleteCommentCommand(Guid CommentId) : ICommand;
