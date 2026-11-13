using Choco.Application.Abstractions.Auditing;
using Choco.Application.Abstractions.Authentication;
using Choco.Contract.Abstractions.Messaging;
using Choco.Domain.AuditLogs;
using Choco.Domain.Primitives.Persistence;
using Choco.Domain.Roles;
using Choco.SharedKernel.Results;

using MediatR;

namespace Choco.Application.Abstractions.Behaviors;

internal sealed class AccessDeniedAuditBehavior<TRequest, TResponse>(
    ICurrentUser currentUser,
    IRoleRepository roleRepository,
    IAuditLogRepository auditLogRepository,
    AuditEntryBuilder entryBuilder,
    IUnitOfWork unitOfWork) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
    where TResponse : Result
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        TResponse response = await next(cancellationToken);

        if (request is ISystemRequest || response.IsSuccess || response.Error != PermissionErrors.Denied)
        {
            return response;
        }

        Result<Guid> actorId = await currentUser.GetIdAsync(cancellationToken);

        if (actorId.IsFailure)
        {
            return response;
        }

        IReadOnlyCollection<string> actorRoles =
            await roleRepository.GetPlatformRoleNamesForReadAsync(actorId.Value, cancellationToken);

        auditLogRepository.Add(entryBuilder.Create(
            AuditActorType.User,
            actorId.Value,
            actorRoles,
            new AuditDescription(
                AuditAction.AccessDenied,
                AuditTargetType.Request,
                actorId.Value,
                Detail: $"Request: {typeof(TRequest).Name}")));

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return response;
    }
}
