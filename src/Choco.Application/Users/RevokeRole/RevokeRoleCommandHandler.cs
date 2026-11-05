using Choco.Application.Abstractions.Authentication;
using Choco.Application.Abstractions.Messaging;
using Choco.Contract.Users;
using Choco.Domain.Primitives.Persistence;
using Choco.Domain.Roles;
using Choco.Domain.Users;
using Choco.SharedKernel.Results;

namespace Choco.Application.Users.RevokeRole;

internal sealed class RevokeRoleCommandHandler : ICommandHandler<RevokeRoleCommand>
{
    private readonly ICurrentUser _currentUser;
    private readonly IUserRepository _userRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly IUnitOfWork _unitOfWork;

    public RevokeRoleCommandHandler(
        ICurrentUser currentUser,
        IUserRepository userRepository,
        IRoleRepository roleRepository,
        IUnitOfWork unitOfWork)
    {
        _currentUser = currentUser;
        _userRepository = userRepository;
        _roleRepository = roleRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(RevokeRoleCommand command, CancellationToken cancellationToken)
    {
        Result<Guid> revokerIdResult = await _currentUser.GetIdAsync(cancellationToken);

        if (revokerIdResult.IsFailure)
        {
            return revokerIdResult;
        }

        User? user = await _userRepository.GetByIdWithRolesAsync(command.UserId, cancellationToken);

        if (user is null)
        {
            return Result.Failure(UserErrors.NotFound);
        }

        Role? role = await _roleRepository.GetByIdAsync(command.RoleId, cancellationToken);

        if (role is null)
        {
            return Result.Failure(RoleErrors.NotFound);
        }

        if (role.IsDefault)
        {
            return Result.Failure(RoleErrors.DefaultNotAssignable);
        }

        IReadOnlyCollection<string> revokerPermissions =
            await _roleRepository.GetPlatformPermissionsForReadAsync(revokerIdResult.Value, cancellationToken);

        if (!role.IsCoveredBy(revokerPermissions))
        {
            return Result.Failure(PermissionErrors.Denied);
        }

        int holderCount = await _userRepository.CountWithRoleAsync(role.Id, cancellationToken);

        Result lastAdministratorCheck = LastAdministratorRule.Check(role, user.HoldsRole(role.Id), holderCount);

        if (lastAdministratorCheck.IsFailure)
        {
            return lastAdministratorCheck;
        }

        user.RevokeRole(role.Id);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
