using Choco.Domain.Roles;
using Choco.SharedKernel.Results;

namespace Choco.Application.Abstractions.Authorization;

public interface IPermissionChecker
{
    Task<Result<bool>> IsGrantedToCurrentUserAsync(
        string permission, PermissionScope scope, CancellationToken cancellationToken = default);
}
