using Choco.Application.Abstractions.Authentication;
using Choco.Application.Abstractions.Messaging;
using Choco.Contract.Abstractions;
using Choco.Contract.Users;
using Choco.Domain.UserBlocks;
using Choco.SharedKernel.Results;

namespace Choco.Application.Users.GetMyBlocks;

internal sealed class GetMyBlocksQueryHandler(ICurrentUser currentUser, IUserBlockRepository blockRepository)
    : IQueryHandler<GetMyBlocksQuery, PageResponse<UserSearchItemResponse>>
{
    public async Task<Result<PageResponse<UserSearchItemResponse>>> Handle(
        GetMyBlocksQuery query, CancellationToken cancellationToken)
    {
        Result<Guid> caller = await currentUser.GetIdAsync(cancellationToken);
        if (caller.IsFailure)
        {
            return Result<PageResponse<UserSearchItemResponse>>.Failure(caller.Error);
        }

        (DateTimeOffset BlockedAt, Guid BlockId)? after =
            BlockCursor.TryDecode(query.Cursor, out BlockCursor? position)
                ? (position!.BlockedAt, position.BlockId)
                : null;

        IReadOnlyList<UserBlockEntry> entries = await blockRepository.ListBlockedForReadAsync(
            caller.Value, after, query.Limit + 1, cancellationToken);

        List<UserBlockEntry> page = entries.Take(query.Limit).ToList();
        string? nextCursor = entries.Count > query.Limit
            ? new BlockCursor(page[^1].BlockedAt, page[^1].BlockId).Encode()
            : null;

        List<UserSearchItemResponse> items = page
            .Select(entry => new UserSearchItemResponse(
                entry.User.Id, entry.User.Username, entry.User.DisplayName, entry.User.AvatarUrl, false))
            .ToList();

        return Result<PageResponse<UserSearchItemResponse>>.Success(new PageResponse<UserSearchItemResponse>(items, nextCursor));
    }
}
