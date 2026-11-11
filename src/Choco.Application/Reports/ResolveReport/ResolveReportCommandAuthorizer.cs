using Choco.Application.Abstractions.Authorization;
using Choco.Contract.Reports;
using Choco.Domain.Roles;

namespace Choco.Application.Reports.ResolveReport;

internal sealed class ResolveReportCommandAuthorizer : IAuthorizer<ResolveReportCommand>
{
    public PermissionRequirement GetRequirement(ResolveReportCommand request)
    {
        return new PermissionRequirement(Permission.HandleReports, PermissionScope.Platform);
    }
}
