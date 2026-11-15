using Choco.Contract.Roles;
using Choco.Domain.Roles;
using Choco.Domain.Users;

namespace Choco.Application.Roles;

internal static class RoleMappings
{
    public static async Task<IReadOnlyList<RoleResponse>> ToResponsesAsync(
        this IRoleRepository repository, IUserRepository userRepository, IReadOnlyList<Role> roles, CancellationToken cancellationToken)
    {
        IReadOnlyList<RoleHolderSummary> summaries = await repository.ListHolderSummariesForReadAsync(
            roles.Select(role => role.Id).ToArray(), cancellationToken);
        Dictionary<Guid, RoleHolderSummary> byRole = summaries.ToDictionary(summary => summary.RoleId);

        Guid[] editorIds = roles.Where(role => role.ModifiedBy.HasValue)
            .Select(role => role.ModifiedBy!.Value).Distinct().ToArray();
        IReadOnlyList<User> editors = await userRepository.ListByIdsIncludingDeletedForReadAsync(editorIds, cancellationToken);
        Dictionary<Guid, string> usernames = editors.ToDictionary(user => user.Id, user => user.Username);

        return roles.Select(role => role.ToResponse(byRole.GetValueOrDefault(role.Id), usernames)).ToArray();
    }

    public static async Task<RoleResponse> ToResponseAsync(
        this IRoleRepository repository, IUserRepository userRepository, Role role, CancellationToken cancellationToken)
    {
        IReadOnlyList<RoleResponse> responses = await repository.ToResponsesAsync(userRepository, [role], cancellationToken);
        return responses[0];
    }

    public static RoleResponse ToResponse(
        this Role role, RoleHolderSummary? summary = null, IReadOnlyDictionary<Guid, string>? usernames = null)
    {
        return new RoleResponse(
            role.Id,
            role.SystemKey,
            role.Name,
            role.IsDefault,
            role.Permissions.Order(StringComparer.Ordinal).ToList(),
            role.Description,
            role.Color,
            role.Icon,
            summary?.Count ?? 0,
            summary?.TopHolders.Select(holder =>
                new RoleHolderResponse(holder.Id, holder.Username, holder.DisplayName, holder.AvatarUrl)).ToArray() ?? [],
            role.ModifiedAt,
            role.ModifiedBy is { } editorId && usernames is not null && usernames.TryGetValue(editorId, out string? username)
                ? new RoleModifiedByResponse(editorId, username)
                : null);
    }
}
