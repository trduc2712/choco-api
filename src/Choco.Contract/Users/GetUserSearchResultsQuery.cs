using Choco.Contract.Abstractions;
using Choco.Contract.Abstractions.Messaging;

namespace Choco.Contract.Users;

/// <summary>Searches for users by username or display name.</summary>
/// <param name="Q">The search text, up to 256 characters after trimming.</param>
/// <param name="Cursor">The previous page's nextCursor for the same search text, or null for the first page.</param>
/// <example>{"q":"username","cursor":null}</example>
public sealed record GetUserSearchResultsQuery(string? Q, string? Cursor) : IQuery<PageResponse<UserSearchItemResponse>>;
