using Choco.Application.Abstractions.Authentication;
using Choco.Application.Abstractions.Messaging;
using Choco.Contract.Users;
using Choco.Domain.Users;
using Choco.SharedKernel.Results;

namespace Choco.Application.Users.GetUserSuggestions;

internal sealed class GetUserSuggestionsQueryHandler(ICurrentUser currentUser, IUserRepository users)
    : IQueryHandler<GetUserSuggestionsQuery, IReadOnlyList<UserSearchItemResponse>>
{
    public async Task<Result<IReadOnlyList<UserSearchItemResponse>>> Handle(
        GetUserSuggestionsQuery query, CancellationToken cancellationToken)
    {
        Result<Guid> caller = await currentUser.GetIdAsync(cancellationToken);
        if (caller.IsFailure)
        {
            return Result<IReadOnlyList<UserSearchItemResponse>>.Failure(caller.Error);
        }

        IReadOnlyList<UserSearchMatch> matches = await UserSearch.ReadAsync(
            users, caller.Value, query.Q?.Trim() ?? string.Empty, null, UserSearch.PageSize, cancellationToken);

        return Result<IReadOnlyList<UserSearchItemResponse>>.Success(matches.Select(match => match.ToResponse()).ToArray());
    }
}
