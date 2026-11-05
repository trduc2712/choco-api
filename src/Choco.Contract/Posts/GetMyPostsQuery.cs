using Choco.Contract.Abstractions;
using Choco.Contract.Abstractions.Messaging;

namespace Choco.Contract.Posts;

public sealed record GetMyPostsQuery(string? Cursor, int Limit) : IQuery<PageResponse<PostResponse>>
{
    public const int DefaultLimit = 20;
    public const int MaxLimit = 50;
}
