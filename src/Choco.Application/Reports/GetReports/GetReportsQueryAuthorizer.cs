using Choco.Application.Abstractions.Authorization;
using Choco.Contract.Reports;
using Choco.Domain.Roles;

namespace Choco.Application.Reports.GetReports;

internal sealed class GetReportsQueryAuthorizer : IAuthorizer<GetReportsQuery>
{
    public PermissionRequirement GetRequirement(GetReportsQuery request)
    {
        return new PermissionRequirement(Permission.HandleReports, PermissionScope.Platform);
    }
}
