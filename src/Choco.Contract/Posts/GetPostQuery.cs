using Choco.Contract.Abstractions.Messaging;

namespace Choco.Contract.Posts;

public sealed record GetPostQuery(Guid Id) : IQuery<PostResponse>;
