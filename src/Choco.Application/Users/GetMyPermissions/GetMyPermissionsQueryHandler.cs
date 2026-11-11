using Choco.Application.Abstractions.Authentication;
using Choco.Application.Abstractions.Messaging;
using Choco.Contract.Users;
using Choco.Domain.Roles;
using Choco.SharedKernel.Results;

namespace Choco.Application.Users.GetMyPermissions;

internal sealed class GetMyPermissionsQueryHandler(ICurrentUser currentUser, IRoleRepository roleRepository)
    : IQueryHandler<GetMyPermissionsQuery, MyPermissionsResponse>
{
    public async Task<Result<MyPermissionsResponse>> Handle(GetMyPermissionsQuery query, CancellationToken cancellationToken)
    {
        Result<Guid> userIdResult = await currentUser.GetIdAsync(cancellationToken);

        if (userIdResult.IsFailure)
        {
            return Result<MyPermissionsResponse>.Failure(userIdResult.Error);
        }

        IReadOnlyCollection<string> permissions =
            await roleRepository.GetPlatformPermissionsForReadAsync(userIdResult.Value, cancellationToken);

        return Result<MyPermissionsResponse>.Success(new MyPermissionsResponse(
            permissions.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList()));
    }
}
