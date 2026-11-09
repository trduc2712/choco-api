using Choco.Contract.Abstractions.Messaging;

namespace Choco.Contract.Posts;

/// <summary>Removes the caller's like from a post.</summary>
/// <param name="PostId">The post's id.</param>
/// <example>{"postId":"00000000-0000-0000-0000-000000000001"}</example>
public sealed record UnlikePostCommand(Guid PostId) : ICommand;
