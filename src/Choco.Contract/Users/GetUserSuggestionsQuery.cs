using Choco.Contract.Abstractions.Messaging;

namespace Choco.Contract.Users;

/// <summary>Gets up to ten user suggestions for the search text.</summary>
/// <param name="Q">The search text, up to 256 characters after trimming.</param>
/// <example>{"q":"username"}</example>
public sealed record GetUserSuggestionsQuery(string? Q) : IQuery<IReadOnlyList<UserSearchItemResponse>>;
