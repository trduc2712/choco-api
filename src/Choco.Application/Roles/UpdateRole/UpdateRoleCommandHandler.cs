using Choco.Application.Abstractions.Auditing;
using Choco.Application.Abstractions.Authentication;
using Choco.Application.Abstractions.Messaging;
using Choco.Contract.Roles;
using Choco.Domain.Primitives.Persistence;
using Choco.Domain.Roles;
using Choco.Domain.Users;
using Choco.SharedKernel.Results;

namespace Choco.Application.Roles.UpdateRole;

internal sealed class UpdateRoleCommandHandler(
    ICurrentUser currentUser,
    IUserRepository userRepository,
    IRoleRepository roleRepository,
    IUnitOfWork unitOfWork,
    AuditChanges auditChanges) : ICommandHandler<UpdateRoleCommand, RoleResponse>
{
    public async Task<Result<RoleResponse>> Handle(UpdateRoleCommand command, CancellationToken cancellationToken)
    {
        Result<Guid> callerIdResult = await currentUser.GetIdAsync(cancellationToken);

        if (callerIdResult.IsFailure)
        {
            return Result<RoleResponse>.Failure(callerIdResult.Error);
        }

        Role? role = await roleRepository.GetByIdAsync(command.Id, cancellationToken);

        if (role is null)
        {
            return Result<RoleResponse>.Failure(RoleErrors.NotFound);
        }

        User? caller = await userRepository.GetByIdWithRolesAsync(callerIdResult.Value, cancellationToken);

        if (caller is null)
        {
            return Result<RoleResponse>.Failure(UserErrors.Deleted);
        }

        IReadOnlyCollection<string> callerPermissions =
            await roleRepository.GetPlatformPermissionsForReadAsync(callerIdResult.Value, cancellationToken);

        Result check = role.CheckCanBeChangedBy(callerPermissions, caller.HoldsRole(role.Id), command.Permissions);

        if (check.IsFailure)
        {
            return Result<RoleResponse>.Failure(check.Error);
        }

        if (await roleRepository.NameExistsAsync(command.Name.Trim(), role.Id, cancellationToken))
        {
            return Result<RoleResponse>.Failure(RoleErrors.NameTaken);
        }

        RoleResponse before = role.ToResponse();
        role.Update(command.Name, command.Permissions);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        RoleResponse after = role.ToResponse();
        return auditChanges.Record(Result<RoleResponse>.Success(after), RoleAuditChanges.Between(before, after));
    }
}
