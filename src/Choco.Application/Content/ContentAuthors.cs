using Choco.Contract.Abstractions;
using Choco.Domain.Users;

namespace Choco.Application.Content;

internal static class ContentAuthors
{
    public static async Task<Dictionary<Guid, string>> LoadUsernamesAsync(
        IUserRepository userRepository, IEnumerable<Guid> authorIds, CancellationToken cancellationToken)
    {
        List<Guid> ids = authorIds.Distinct().ToList();

        if (ids.Count == 0)
        {
            return [];
        }

        IReadOnlyList<User> authors = await userRepository.ListByIdsIncludingDeletedForReadAsync(ids, cancellationToken);

        return authors.ToDictionary(author => author.Id, author => author.Username);
    }

    public static ContentAuthorResponse ToResponse(Guid authorId, Dictionary<Guid, string> usernames)
    {
        return new ContentAuthorResponse(authorId, usernames.GetValueOrDefault(authorId));
    }
}
