using Choco.Application.Abstractions.Authentication;
using Choco.Application.Abstractions.Messaging;
using Choco.Contract.Abstractions;
using Choco.Contract.Users;
using Choco.Domain.Users;
using Choco.SharedKernel.Results;

namespace Choco.Application.Users.GetUserSearchResults;

internal sealed class GetUserSearchResultsQueryHandler(ICurrentUser currentUser, IUserRepository users)
    : IQueryHandler<GetUserSearchResultsQuery, PageResponse<UserSearchItemResponse>>
{
    public async Task<Result<PageResponse<UserSearchItemResponse>>> Handle(
        GetUserSearchResultsQuery query, CancellationToken cancellationToken)
    {
        Result<Guid> caller = await currentUser.GetIdAsync(cancellationToken);
        if (caller.IsFailure)
        {
            return Result<PageResponse<UserSearchItemResponse>>.Failure(caller.Error);
        }

        string text = query.Q?.Trim() ?? string.Empty;
        (int Rank, string Username, Guid Id)? after =
            UserSearchCursor.TryDecode(query.Cursor, text, out var position) ? position : null;
        IReadOnlyList<UserSearchMatch> matches = await UserSearch.ReadAsync(
            users, caller.Value, text, after, UserSearch.PageSize + 1, cancellationToken);

        UserSearchItemResponse[] items = matches.Take(UserSearch.PageSize).Select(match => match.ToResponse()).ToArray();
        string? nextCursor = matches.Count > UserSearch.PageSize
            ? UserSearchCursor.Encode(matches[UserSearch.PageSize - 1], text)
            : null;

        return Result<PageResponse<UserSearchItemResponse>>.Success(new(items, nextCursor));
    }
}
