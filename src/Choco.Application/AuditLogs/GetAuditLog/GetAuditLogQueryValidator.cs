using Choco.Contract.AuditLogs;

using FluentValidation;

namespace Choco.Application.AuditLogs.GetAuditLog;

internal sealed class GetAuditLogQueryValidator : AbstractValidator<GetAuditLogQuery>
{
    public GetAuditLogQueryValidator()
    {
        RuleFor(query => query.Id).NotEqual(Guid.Empty);
    }
}
