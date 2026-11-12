using Choco.Contract.Abstractions;
using Choco.Contract.Abstractions.Messaging;

namespace Choco.Contract.Posts;

/// <summary>Lists posts for administrators, newest first.</summary>
/// <param name="State">Only posts in this state: visible or deleted. Null for visible posts.</param>
/// <param name="AuthorId">Only posts written by this user. Null for any author.</param>
/// <param name="From">Only posts created at or after this time. Null for no lower limit.</param>
/// <param name="To">Only posts created before this time. Null for no upper limit.</param>
/// <param name="Page">The page number, from 1. Defaults to 1 at the endpoint.</param>
/// <param name="PageSize">The page size, from 1 to 50. Defaults to 20 at the endpoint.</param>
/// <param name="Q">Words that must all appear in the content. Null or blank for no filter.</param>
/// <example>{"state":"visible","authorId":null,"from":"2026-01-01T00:00:00+00:00","to":null,"page":1,"pageSize":20,"q":"hello world"}</example>
public sealed record GetAdminPostsQuery(
    string? State, Guid? AuthorId, DateTimeOffset? From, DateTimeOffset? To, int Page, int PageSize, string? Q = null)
    : IQuery<OffsetPageResponse<AdminPostResponse>>
{
    public const int DefaultPage = 1;
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 50;
    public const int SearchMaxLength = 100;
    public const int MaxSearchTerms = 5;
}
