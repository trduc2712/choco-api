using Choco.Contract.Abstractions.Messaging;

namespace Choco.Contract.Posts;

/// <summary>
/// A post to publish.
/// </summary>
/// <param name="Content">The post text. Required, at most 500 characters once leading and trailing whitespace is removed.</param>
/// <example>{ "content": "content" }</example>
public sealed record CreatePostCommand(string Content) : ICommand<PostResponse>;
