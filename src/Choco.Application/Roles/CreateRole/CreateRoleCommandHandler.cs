using Choco.Application.Abstractions.Authentication;
using Choco.Application.Abstractions.Messaging;
using Choco.Contract.Roles;
using Choco.Domain.Primitives.Persistence;
using Choco.Domain.Roles;
using Choco.SharedKernel.Results;

namespace Choco.Application.Roles.CreateRole;

internal sealed class CreateRoleCommandHandler(
    ICurrentUser currentUser,
    IRoleRepository roleRepository,
    IUnitOfWork unitOfWork) : ICommandHandler<CreateRoleCommand, RoleResponse>
{
    public async Task<Result<RoleResponse>> Handle(CreateRoleCommand command, CancellationToken cancellationToken)
    {
        Result<Guid> creatorIdResult = await currentUser.GetIdAsync(cancellationToken);

        if (creatorIdResult.IsFailure)
        {
            return Result<RoleResponse>.Failure(creatorIdResult.Error);
        }

        Role role = Role.Create(command.Name, command.Permissions);

        IReadOnlyCollection<string> creatorPermissions =
            await roleRepository.GetPlatformPermissionsForReadAsync(creatorIdResult.Value, cancellationToken);

        if (!role.IsCoveredBy(creatorPermissions))
        {
            return Result<RoleResponse>.Failure(PermissionErrors.Denied);
        }

        if (await roleRepository.NameExistsAsync(role.Name, cancellationToken: cancellationToken))
        {
            return Result<RoleResponse>.Failure(RoleErrors.NameTaken);
        }

        roleRepository.Add(role);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<RoleResponse>.Success(role.ToResponse());
    }
}
