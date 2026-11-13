using Choco.Application.Abstractions.Auditing;
using Choco.Application.Abstractions.Authorization;
using Choco.Contract.Users;
using Choco.Domain.AuditLogs;
using Choco.Domain.Roles;
using Choco.Domain.Users;
using Choco.SharedKernel.Results;

using Microsoft.Extensions.Options;

namespace Choco.Application.Users.BootstrapAdministrators;

internal sealed class BootstrapAdministratorsCommandAuditDescriber(
    IOptions<AdministratorBootstrapOptions> options,
    IUserRepository userRepository,
    IRoleRepository roleRepository) : IAuditDescriber<BootstrapAdministratorsCommand, Result>
{
    private HashSet<Guid> _alreadyAdministrators = [];

    public async Task CaptureAsync(BootstrapAdministratorsCommand request, CancellationToken cancellationToken)
    {
        if (options.Value.BootstrapAdminIdentityIds.Count == 0)
        {
            return;
        }

        Role? adminRole = await roleRepository.GetBySystemKeyAsync(SystemRoles.Admin, cancellationToken);

        if (adminRole is null)
        {
            return;
        }

        IReadOnlyList<User> users = await userRepository.ListByIdentityIdsWithRolesAsync(
            options.Value.BootstrapAdminIdentityIds, cancellationToken);

        _alreadyAdministrators = users.Where(user => user.HoldsRole(adminRole.Id)).Select(user => user.Id).ToHashSet();
    }

    public Task<AuditDescription> DescribeAsync(
        BootstrapAdministratorsCommand request, Result response, CancellationToken cancellationToken)
    {
        throw new NotSupportedException("The command is described by one entry for each administrator granted.");
    }

    public async Task<IReadOnlyList<AuditDescription>> DescribeAllAsync(
        BootstrapAdministratorsCommand request, Result response, CancellationToken cancellationToken)
    {
        if (options.Value.BootstrapAdminIdentityIds.Count == 0)
        {
            return [];
        }

        Role? adminRole = await roleRepository.GetBySystemKeyAsync(SystemRoles.Admin, cancellationToken);

        if (adminRole is null)
        {
            return [];
        }

        IReadOnlyList<User> users = await userRepository.ListByIdentityIdsWithRolesAsync(
            options.Value.BootstrapAdminIdentityIds, cancellationToken);

        return
        [
            .. users
                .Where(user => user.HoldsRole(adminRole.Id) && !_alreadyAdministrators.Contains(user.Id))
                .Select(user => new AuditDescription(
                    AuditAction.RoleAssigned, AuditTargetType.User, user.Id, user.Id, $"Role: {adminRole.Name}"))
        ];
    }
}
