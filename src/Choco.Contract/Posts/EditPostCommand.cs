using Choco.Contract.Abstractions.Messaging;

namespace Choco.Contract.Posts;

/// <summary>
/// The new text of a post.
/// </summary>
/// <param name="Id">Ignored; the post is the one named in the route.</param>
/// <param name="Content">The post text. Required, at most 500 characters once leading and trailing whitespace is removed.</param>
/// <example>{ "content": "edited-content" }</example>
public sealed record EditPostCommand(Guid Id, string Content) : ICommand<PostResponse>;
