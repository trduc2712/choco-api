using Choco.Application.Abstractions.Authorization;
using Choco.Domain.Roles;
using Choco.SharedKernel.Results;

using MediatR;

namespace Choco.Application.Abstractions.Behaviors;

internal sealed class AuthorizationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
    where TResponse : Result
{
    private readonly IEnumerable<IAuthorizer<TRequest>> _authorizers;
    private readonly IPermissionChecker _permissionChecker;

    public AuthorizationBehavior(IEnumerable<IAuthorizer<TRequest>> authorizers, IPermissionChecker permissionChecker)
    {
        _authorizers = authorizers;
        _permissionChecker = permissionChecker;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        foreach (IAuthorizer<TRequest> authorizer in _authorizers)
        {
            PermissionRequirement requirement = authorizer.GetRequirement(request);

            Result<bool> isGranted = await _permissionChecker.IsGrantedToCurrentUserAsync(
                requirement.Permission, requirement.Scope, cancellationToken);

            if (isGranted.IsFailure)
            {
                return FailureResult.Create<TResponse>(isGranted.Error);
            }

            if (!isGranted.Value)
            {
                return FailureResult.Create<TResponse>(PermissionErrors.Denied);
            }
        }

        return await next(cancellationToken);
    }
}
