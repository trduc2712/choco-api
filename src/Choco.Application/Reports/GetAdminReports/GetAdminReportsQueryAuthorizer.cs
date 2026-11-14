using Choco.Application.Abstractions.Authorization;
using Choco.Contract.Reports;
using Choco.Domain.Roles;

namespace Choco.Application.Reports.GetAdminReports;

internal sealed class GetAdminReportsQueryAuthorizer : IAuthorizer<GetAdminReportsQuery>
{
    public PermissionRequirement GetRequirement(GetAdminReportsQuery request)
    {
        return new PermissionRequirement(Permission.HandleReports, PermissionScope.Platform);
    }
}
