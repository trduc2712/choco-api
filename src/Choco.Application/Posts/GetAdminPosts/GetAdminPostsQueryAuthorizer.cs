using Choco.Application.Abstractions.Authorization;
using Choco.Contract.Posts;
using Choco.Domain.Roles;

namespace Choco.Application.Posts.GetAdminPosts;

internal sealed class GetAdminPostsQueryAuthorizer : IAuthorizer<GetAdminPostsQuery>
{
    public PermissionRequirement GetRequirement(GetAdminPostsQuery request)
    {
        return new PermissionRequirement(Permission.ReadAnyContent, PermissionScope.Platform);
    }
}
