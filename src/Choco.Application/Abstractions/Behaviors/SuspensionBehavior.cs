using Choco.Application.Abstractions.Authentication;
using Choco.Contract.Abstractions.Messaging;
using Choco.Domain.Users;
using Choco.SharedKernel.Results;

using MediatR;

namespace Choco.Application.Abstractions.Behaviors;

internal sealed class SuspensionBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
    where TResponse : Result
{
    private readonly ICurrentUser _currentUser;

    public SuspensionBehavior(ICurrentUser currentUser)
    {
        _currentUser = currentUser;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (request is not IBaseCommand || request is ISystemRequest)
        {
            return await next(cancellationToken);
        }

        Result<CurrentUserSuspension?> suspension = await _currentUser.GetSuspensionAsync(cancellationToken);

        if (suspension.IsFailure)
        {
            return FailureResult.Create<TResponse>(suspension.Error);
        }

        if (suspension.Value is { } current)
        {
            return FailureResult.Create<TResponse>(UserErrors.Suspended(current.Reason, current.SuspendedAt));
        }

        return await next(cancellationToken);
    }
}
