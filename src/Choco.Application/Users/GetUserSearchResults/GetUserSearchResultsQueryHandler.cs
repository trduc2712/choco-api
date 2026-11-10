using Choco.Application.Abstractions.Authentication;
using Choco.Application.Abstractions.Messaging;
using Choco.Contract.Abstractions;
using Choco.Contract.Users;
using Choco.Domain.UserFollows;
using Choco.Domain.Users;
using Choco.SharedKernel.Results;

namespace Choco.Application.Users.GetUserSearchResults;

internal sealed class GetUserSearchResultsQueryHandler(
    ICurrentUser currentUser, IUserRepository users, IUserFollowRepository followRepository)
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

        UserSearchMatch[] page = matches.Take(UserSearch.PageSize).ToArray();
        IReadOnlySet<Guid> followed = await FollowStatus.ReadFollowedAsync(
            followRepository, caller.Value, page.Select(match => match.Id).ToArray(), cancellationToken);
        UserSearchItemResponse[] items = page.Select(match => match.ToResponse(followed)).ToArray();
        string? nextCursor = matches.Count > UserSearch.PageSize
            ? UserSearchCursor.Encode(matches[UserSearch.PageSize - 1], text)
            : null;

        return Result<PageResponse<UserSearchItemResponse>>.Success(new(items, nextCursor));
    }
}
