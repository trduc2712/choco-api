using Choco.Application.Abstractions.Authentication;
using Choco.Application.Abstractions.Messaging;
using Choco.Contract.Roles;
using Choco.Domain.Primitives.Persistence;
using Choco.Domain.Roles;
using Choco.Domain.Users;
using Choco.SharedKernel.Results;

namespace Choco.Application.Roles.DeleteRole;

internal sealed class DeleteRoleCommandHandler(
    ICurrentUser currentUser,
    IUserRepository userRepository,
    IRoleRepository roleRepository,
    IUnitOfWork unitOfWork) : ICommandHandler<DeleteRoleCommand, RoleResponse>
{
    public async Task<Result<RoleResponse>> Handle(DeleteRoleCommand command, CancellationToken cancellationToken)
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

        Result check = role.CheckCanBeChangedBy(callerPermissions, caller.HoldsRole(role.Id), []);

        if (check.IsFailure)
        {
            return Result<RoleResponse>.Failure(check.Error);
        }

        Result deletable = role.CheckCanBeDeleted();

        if (deletable.IsFailure)
        {
            return Result<RoleResponse>.Failure(deletable.Error);
        }

        RoleResponse deleted = role.ToResponse();

        roleRepository.Remove(role);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<RoleResponse>.Success(deleted);
    }
}
