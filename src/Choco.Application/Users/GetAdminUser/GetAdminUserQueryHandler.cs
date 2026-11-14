using Choco.Application.Abstractions.Messaging;
using Choco.Contract.Users;
using Choco.Domain.Reports;
using Choco.Domain.Roles;
using Choco.Domain.Users;
using Choco.SharedKernel.Results;

namespace Choco.Application.Users.GetAdminUser;

internal sealed class GetAdminUserQueryHandler(
    IUserRepository userRepository,
    IRoleRepository roleRepository,
    IReportRepository reportRepository) : IQueryHandler<GetAdminUserQuery, AdminUserDetailResponse>
{
    public async Task<Result<AdminUserDetailResponse>> Handle(
        GetAdminUserQuery query, CancellationToken cancellationToken)
    {
        User? user = await userRepository.GetByIdWithRolesIncludingDeletedForReadAsync(query.UserId, cancellationToken);

        if (user is null)
        {
            return Result<AdminUserDetailResponse>.Failure(UserErrors.NotFound);
        }

        Dictionary<Guid, string> roleNames = user.Roles.Count == 0
            ? []
            : (await roleRepository.ListForReadAsync(cancellationToken)).ToDictionary(role => role.Id, role => role.Name);

        AdminUserSuspensionResponse? suspension = null;

        if (user.SuspendedAt is { } suspendedAt && user.SuspensionReason is { } reason)
        {
            AdminUserReferenceResponse? suspendedBy = null;

            if (user.SuspendedBy is { } suspendedById)
            {
                IReadOnlyList<User> suspenders = await userRepository.ListByIdsIncludingDeletedForReadAsync(
                    [suspendedById], cancellationToken);

                suspendedBy = suspenders.Count == 0
                    ? null
                    : new AdminUserReferenceResponse(suspenders[0].Id, suspenders[0].Username);
            }

            suspension = new AdminUserSuspensionResponse(reason, suspendedAt, suspendedBy);
        }

        int openReportCount = await reportRepository.CountOpenAboutUserAsync(user.Id, cancellationToken);

        return Result<AdminUserDetailResponse>.Success(new AdminUserDetailResponse(
            user.Id,
            user.Username,
            user.DisplayName,
            user.Bio,
            user.AvatarUrl,
            user.CreatedAt,
            user.Roles
                .Where(userRole => roleNames.ContainsKey(userRole.RoleId))
                .Select(userRole => new AdminUserRoleResponse(userRole.RoleId, roleNames[userRole.RoleId]))
                .OrderBy(role => role.Name, StringComparer.Ordinal)
                .ToList(),
            user.IsSuspended,
            suspension,
            user.IsDeleted,
            user.DeletedAt,
            openReportCount));
    }
}
