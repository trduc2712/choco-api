using Choco.SharedKernel.Results;

namespace Choco.Domain.AuditLogs;

public static class AuditLogErrors
{
    public static readonly Error NotFound = Error.NotFound("AuditLog.NotFound", "The audit log entry was not found.");
}
