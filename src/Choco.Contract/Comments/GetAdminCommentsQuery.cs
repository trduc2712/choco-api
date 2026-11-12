using Choco.Contract.Abstractions;
using Choco.Contract.Abstractions.Messaging;

namespace Choco.Contract.Comments;

/// <summary>Lists comments for administrators, newest first.</summary>
/// <param name="State">Only comments in this state: visible or deleted. Null for visible comments.</param>
/// <param name="AuthorId">Only comments written by this user. Null for any author.</param>
/// <param name="PostId">Only comments on this post. Null for any post.</param>
/// <param name="From">Only comments created at or after this time. Null for no lower limit.</param>
/// <param name="To">Only comments created before this time. Null for no upper limit.</param>
/// <param name="Page">The page number, from 1. Defaults to 1 at the endpoint.</param>
/// <param name="PageSize">The page size, from 1 to 50. Defaults to 20 at the endpoint.</param>
/// <param name="Q">Words that must all appear in the content. Null or blank for no filter.</param>
/// <example>{"state":"visible","authorId":null,"postId":null,"from":null,"to":null,"page":1,"pageSize":20,"q":"hello world"}</example>
public sealed record GetAdminCommentsQuery(
    string? State,
    Guid? AuthorId,
    Guid? PostId,
    DateTimeOffset? From,
    DateTimeOffset? To,
    int Page,
    int PageSize,
    string? Q = null) : IQuery<OffsetPageResponse<AdminCommentResponse>>
{
    public const int DefaultPage = 1;
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 50;
    public const int SearchMaxLength = 100;
    public const int MaxSearchTerms = 5;
}
