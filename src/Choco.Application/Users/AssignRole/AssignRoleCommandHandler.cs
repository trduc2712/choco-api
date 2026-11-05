using Choco.Application.Abstractions.Authentication;
using Choco.Application.Abstractions.Messaging;
using Choco.Contract.Users;
using Choco.Domain.Primitives.Persistence;
using Choco.Domain.Roles;
using Choco.Domain.Users;
using Choco.SharedKernel.Results;

namespace Choco.Application.Users.AssignRole;

internal sealed class AssignRoleCommandHandler : ICommandHandler<AssignRoleCommand>
{
    private readonly ICurrentUser _currentUser;
    private readonly IUserRepository _userRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public AssignRoleCommandHandler(
        ICurrentUser currentUser,
        IUserRepository userRepository,
        IRoleRepository roleRepository,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider)
    {
        _currentUser = currentUser;
        _userRepository = userRepository;
        _roleRepository = roleRepository;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
    }

    public async Task<Result> Handle(AssignRoleCommand command, CancellationToken cancellationToken)
    {
        Result<Guid> assignerIdResult = await _currentUser.GetIdAsync(cancellationToken);

        if (assignerIdResult.IsFailure)
        {
            return assignerIdResult;
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

        IReadOnlyCollection<string> assignerPermissions =
            await _roleRepository.GetPlatformPermissionsForReadAsync(assignerIdResult.Value, cancellationToken);

        if (!role.IsCoveredBy(assignerPermissions))
        {
            return Result.Failure(PermissionErrors.Denied);
        }

        user.AssignRole(role.Id, assignerIdResult.Value, _timeProvider.GetUtcNow());

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
