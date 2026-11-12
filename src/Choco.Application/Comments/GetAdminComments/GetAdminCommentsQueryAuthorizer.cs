using Choco.Application.Abstractions.Authorization;
using Choco.Contract.Comments;
using Choco.Domain.Roles;

namespace Choco.Application.Comments.GetAdminComments;

internal sealed class GetAdminCommentsQueryAuthorizer : IAuthorizer<GetAdminCommentsQuery>
{
    public PermissionRequirement GetRequirement(GetAdminCommentsQuery request)
    {
        return new PermissionRequirement(Permission.ReadAnyContent, PermissionScope.Platform);
    }
}
