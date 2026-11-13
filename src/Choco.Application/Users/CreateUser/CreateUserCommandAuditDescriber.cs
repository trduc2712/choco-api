using Choco.Application.Abstractions.Auditing;
using Choco.Contract.Users;
using Choco.Domain.AuditLogs;
using Choco.Domain.Users;
using Choco.SharedKernel.Results;

namespace Choco.Application.Users.CreateUser;

internal sealed class CreateUserCommandAuditDescriber(IUserRepository userRepository)
    : IAuditDescriber<CreateUserCommand, Result>
{
    private bool _existed;

    public async Task CaptureAsync(CreateUserCommand request, CancellationToken cancellationToken)
    {
        _existed = await userRepository.ExistsByIdentityIdIncludingDeletedAsync(request.IdentityId, cancellationToken);
    }

    public async Task<AuditDescription> DescribeAsync(
        CreateUserCommand request, Result response, CancellationToken cancellationToken)
    {
        User user = await userRepository.GetByIdentityIdIncludingDeletedAsync(request.IdentityId, cancellationToken)
            ?? throw new InvalidOperationException("The user created by the command is missing.");

        return new AuditDescription(AuditAction.UserCreated, AuditTargetType.User, user.Id, user.Id);
    }

    public async Task<IReadOnlyList<AuditDescription>> DescribeAllAsync(
        CreateUserCommand request, Result response, CancellationToken cancellationToken)
    {
        return _existed ? [] : [await DescribeAsync(request, response, cancellationToken)];
    }
}
