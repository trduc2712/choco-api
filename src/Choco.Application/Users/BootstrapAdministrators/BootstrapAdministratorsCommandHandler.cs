using Choco.Application.Abstractions.Authorization;
using Choco.Application.Abstractions.Messaging;
using Choco.Contract.Users;
using Choco.Domain.Primitives.Persistence;
using Choco.Domain.Roles;
using Choco.Domain.Users;
using Choco.SharedKernel.Results;

using Microsoft.Extensions.Options;

namespace Choco.Application.Users.BootstrapAdministrators;

internal sealed class BootstrapAdministratorsCommandHandler : ICommandHandler<BootstrapAdministratorsCommand>
{
    private readonly IOptions<AdministratorBootstrapOptions> _options;
    private readonly IUserRepository _userRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public BootstrapAdministratorsCommandHandler(
        IOptions<AdministratorBootstrapOptions> options,
        IUserRepository userRepository,
        IRoleRepository roleRepository,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider)
    {
        _options = options;
        _userRepository = userRepository;
        _roleRepository = roleRepository;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
    }

    public async Task<Result> Handle(BootstrapAdministratorsCommand command, CancellationToken cancellationToken)
    {
        List<string> identityIds = _options.Value.BootstrapAdminIdentityIds;

        if (identityIds.Count == 0)
        {
            return Result.Success();
        }

        Role adminRole = await _roleRepository.GetBySystemKeyAsync(SystemRoles.Admin, cancellationToken)
            ?? throw new InvalidOperationException($"The '{SystemRoles.Admin}' system role is missing.");

        IReadOnlyList<User> users = await _userRepository.ListByIdentityIdsWithRolesAsync(identityIds, cancellationToken);
        DateTimeOffset now = _timeProvider.GetUtcNow();

        foreach (User user in users)
        {
            user.AssignRole(adminRole.Id, null, now);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
