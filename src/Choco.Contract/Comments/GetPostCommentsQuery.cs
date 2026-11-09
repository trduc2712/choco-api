using Choco.Contract.Abstractions;
using Choco.Contract.Abstractions.Messaging;

namespace Choco.Contract.Comments;

/// <summary>Lists the comments on a post, oldest first.</summary>
/// <param name="PostId">The post's non-empty GUID.</param>
/// <param name="Cursor">The previous page's nextCursor, or null for the first page.</param>
/// <param name="Limit">The page size, from 1 to 50. Defaults to 20 at the endpoint.</param>
/// <example>{ "postId": "00000000-0000-0000-0000-000000000001", "cursor": null, "limit": 20 }</example>
public sealed record GetPostCommentsQuery(Guid PostId, string? Cursor, int Limit) : IQuery<PageResponse<CommentResponse>>;
