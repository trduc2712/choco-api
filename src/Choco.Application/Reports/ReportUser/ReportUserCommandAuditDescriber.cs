using Choco.Application.Abstractions.Auditing;
using Choco.Contract.Reports;
using Choco.Domain.AuditLogs;
using Choco.SharedKernel.Results;

namespace Choco.Application.Reports.ReportUser;

internal sealed class ReportUserCommandAuditDescriber : IAuditDescriber<ReportUserCommand, Result>
{
    public Task<AuditDescription> DescribeAsync(
        ReportUserCommand request, Result response, CancellationToken cancellationToken)
    {
        return Task.FromResult(new AuditDescription(
            AuditAction.UserReported, AuditTargetType.User, request.UserId, request.UserId, $"Reason: {request.Reason}"));
    }
}
