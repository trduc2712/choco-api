using Choco.Application.Abstractions.Auditing;
using Choco.Application.Abstractions.Authentication;
using Choco.Application.Abstractions.Requests;
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
    private readonly IRequestContext _requestContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public AuditBehavior(
        IEnumerable<IAuditDescriber<TRequest, TResponse>> describers,
        ICurrentUser currentUser,
        IRoleRepository roleRepository,
        IAuditLogRepository auditLogRepository,
        IRequestContext requestContext,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider)
    {
        _describers = describers;
        _currentUser = currentUser;
        _roleRepository = roleRepository;
        _auditLogRepository = auditLogRepository;
        _requestContext = requestContext;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
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
            Result<Guid> actorIdResult = await _currentUser.GetIdAsync(cancellationToken);

            if (actorIdResult.IsFailure)
            {
                return FailureResult.Create<TResponse>(actorIdResult.Error);
            }

            IReadOnlyCollection<string> actorRoles =
                await _roleRepository.GetPlatformRoleNamesForReadAsync(actorIdResult.Value, cancellationToken);

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
                Guid? affectedUserId = description.AffectedUserId == actorIdResult.Value
                    ? null
                    : description.AffectedUserId;

                AuditLogEntry entry = AuditLogEntry.Create(
                    _timeProvider.GetUtcNow(),
                    AuditActorType.User,
                    actorIdResult.Value,
                    actorRoles,
                    _requestContext.ClientId,
                    description.Action,
                    description.TargetType,
                    description.TargetId,
                    affectedUserId,
                    description.Detail,
                    _requestContext.TraceId,
                    _requestContext.UserAgent,
                    _requestContext.IpAddress);

                _auditLogRepository.Add(entry);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return response;
        }, cancellationToken);
    }
}
