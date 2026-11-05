using Choco.Application.Abstractions.Authorization;
using Choco.Application.Abstractions.Messaging;
using Choco.Contract.Users;
using Choco.Domain.Primitives.Persistence;
using Choco.Domain.Roles;
using Choco.Domain.Users;
using Choco.SharedKernel.Results;

using Microsoft.Extensions.Options;

namespace Choco.Application.Users.CreateUser;

internal sealed class CreateUserCommandHandler : ICommandHandler<CreateUserCommand>
{
    private readonly IUserRepository _userRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly IOptions<AdministratorBootstrapOptions> _bootstrapOptions;
    private readonly TimeProvider _timeProvider;
    private readonly IUnitOfWork _unitOfWork;

    public CreateUserCommandHandler(
        IUserRepository userRepository,
        IRoleRepository roleRepository,
        IOptions<AdministratorBootstrapOptions> bootstrapOptions,
        TimeProvider timeProvider,
        IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _roleRepository = roleRepository;
        _bootstrapOptions = bootstrapOptions;
        _timeProvider = timeProvider;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(CreateUserCommand command, CancellationToken cancellationToken)
    {
        if (await _userRepository.ExistsByIdentityIdIncludingDeletedAsync(command.IdentityId, cancellationToken))
        {
            return Result.Success();
        }

        User user = User.Create(command.IdentityId, command.Username, command.DisplayName, command.AvatarUrl);

        if (_bootstrapOptions.Value.BootstrapAdminIdentityIds.Contains(command.IdentityId))
        {
            Role adminRole = await _roleRepository.GetBySystemKeyAsync(SystemRoles.Admin, cancellationToken)
                ?? throw new InvalidOperationException($"The '{SystemRoles.Admin}' system role is missing.");

            user.AssignRole(adminRole.Id, null, _timeProvider.GetUtcNow());
        }

        _userRepository.Add(user);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
