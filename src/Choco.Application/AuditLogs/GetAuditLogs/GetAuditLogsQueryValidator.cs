using Choco.Contract.AuditLogs;
using Choco.Domain.AuditLogs;

using FluentValidation;

namespace Choco.Application.AuditLogs.GetAuditLogs;

internal sealed class GetAuditLogsQueryValidator : AbstractValidator<GetAuditLogsQuery>
{
    public GetAuditLogsQueryValidator()
    {
        RuleFor(query => query.Page).GreaterThanOrEqualTo(1);

        RuleFor(query => query.PageSize).InclusiveBetween(1, GetAuditLogsQuery.MaxPageSize);

        RuleFor(query => query.ActorId)
            .NotEqual(Guid.Empty)
            .When(query => query.ActorId is not null);

        RuleFor(query => query.TargetId)
            .NotEqual(Guid.Empty)
            .When(query => query.TargetId is not null);

        RuleFor(query => query.AffectedUserId)
            .NotEqual(Guid.Empty)
            .When(query => query.AffectedUserId is not null);

        RuleFor(query => query.Action)
            .Must(action => AuditLogEnums.TryParse<AuditAction>(action, out _))
            .When(query => query.Action is not null)
            .WithMessage($"'{{PropertyName}}' must be one of: {AuditLogEnums.Options<AuditAction>()}.");

        RuleFor(query => query.TargetType)
            .Must(targetType => AuditLogEnums.TryParse<AuditTargetType>(targetType, out _))
            .When(query => query.TargetType is not null)
            .WithMessage($"'{{PropertyName}}' must be one of: {AuditLogEnums.Options<AuditTargetType>()}.");

        RuleFor(query => query.To)
            .GreaterThan(query => query.From!.Value)
            .When(query => query.From is not null && query.To is not null)
            .WithMessage("'{PropertyName}' must be later than 'From'.");
    }
}
