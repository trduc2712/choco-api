using Choco.Application.Abstractions.Auditing;
using Choco.Application.Abstractions.Authentication;
using Choco.Contract.Abstractions.Messaging;
using Choco.Domain.AuditLogs;
using Choco.Domain.Primitives.Persistence;
using Choco.Domain.Roles;
using Choco.SharedKernel.Results;

using MediatR;

namespace Choco.Application.Abstractions.Behaviors;

internal sealed class AuditBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
    where TResponse : Result
{
    private readonly IEnumerable<IAuditDescriber<TRequest, TResponse>> _describers;
    private readonly ICurrentUser _currentUser;
    private readonly IRoleRepository _roleRepository;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly AuditEntryBuilder _entryBuilder;
    private readonly IUnitOfWork _unitOfWork;

    public AuditBehavior(
        IEnumerable<IAuditDescriber<TRequest, TResponse>> describers,
        ICurrentUser currentUser,
        IRoleRepository roleRepository,
        IAuditLogRepository auditLogRepository,
        AuditEntryBuilder entryBuilder,
        IUnitOfWork unitOfWork)
    {
        _describers = describers;
        _currentUser = currentUser;
        _roleRepository = roleRepository;
        _auditLogRepository = auditLogRepository;
        _entryBuilder = entryBuilder;
        _unitOfWork = unitOfWork;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        IAuditDescriber<TRequest, TResponse>? describer = _describers.SingleOrDefault();

        if (describer is null)
        {
            return await next(cancellationToken);
        }

        return await _unitOfWork.InTransactionAsync(async () =>
        {
            bool isSystem = request is ISystemRequest;
            Guid? actorId = null;
            IReadOnlyCollection<string> actorRoles = [];

            if (!isSystem)
            {
                Result<Guid> actorIdResult = await _currentUser.GetIdAsync(cancellationToken);

                if (actorIdResult.IsFailure)
                {
                    return FailureResult.Create<TResponse>(actorIdResult.Error);
                }

                actorId = actorIdResult.Value;
                actorRoles = await _roleRepository.GetPlatformRoleNamesForReadAsync(actorId.Value, cancellationToken);
            }

            await describer.CaptureAsync(request, cancellationToken);

            TResponse response = await next(cancellationToken);

            if (response.IsFailure)
            {
                return response;
            }

            IReadOnlyList<AuditDescription> descriptions =
                await describer.DescribeAllAsync(request, response, cancellationToken);

            foreach (AuditDescription description in descriptions)
            {
                AuditLogEntry entry = _entryBuilder.Create(
                    isSystem ? AuditActorType.System : AuditActorType.User, actorId, actorRoles, description);

                _auditLogRepository.Add(entry);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return response;
        }, cancellationToken);
    }
}
