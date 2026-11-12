using Choco.Application.Abstractions.Messaging;
using Choco.Contract.Abstractions;
using Choco.Contract.Users;
using Choco.Domain.Roles;
using Choco.Domain.Users;
using Choco.SharedKernel.Results;

namespace Choco.Application.Users.GetAdminUsers;

internal sealed class GetAdminUsersQueryHandler(
    IUserRepository userRepository,
    IRoleRepository roleRepository) : IQueryHandler<GetAdminUsersQuery, OffsetPageResponse<AdminUserResponse>>
{
    public async Task<Result<OffsetPageResponse<AdminUserResponse>>> Handle(
        GetAdminUsersQuery query, CancellationToken cancellationToken)
    {
        UserState state = UserStates.TryParse(query.State, out UserState parsedState) ? parsedState : UserState.Active;
        string? text = string.IsNullOrWhiteSpace(query.Q) ? null : query.Q.Trim();
        var filter = new UserListFilter(text, query.RoleId, state);
        long skip = (long)(query.Page - 1) * query.PageSize;

        int totalCount = await userRepository.CountIncludingDeletedForReadAsync(filter, cancellationToken);
        IReadOnlyList<User> users = skip >= totalCount
            ? []
            : await userRepository.ListWithRolesIncludingDeletedForReadAsync(filter, (int)skip, query.PageSize, cancellationToken);

        Dictionary<Guid, string> roleNames = users.Count == 0
            ? []
            : (await roleRepository.ListForReadAsync(cancellationToken)).ToDictionary(role => role.Id, role => role.Name);

        List<AdminUserResponse> items = users
            .Select(user => new AdminUserResponse(
                user.Id,
                user.Username,
                user.DisplayName,
                user.CreatedAt,
                user.Roles
                    .Where(userRole => roleNames.ContainsKey(userRole.RoleId))
                    .Select(userRole => new AdminUserRoleResponse(userRole.RoleId, roleNames[userRole.RoleId]))
                    .OrderBy(role => role.Name, StringComparer.Ordinal)
                    .ToList(),
                user.IsDeleted))
            .ToList();

        return Result<OffsetPageResponse<AdminUserResponse>>.Success(
            new OffsetPageResponse<AdminUserResponse>(items, query.Page, query.PageSize, totalCount));
    }
}
