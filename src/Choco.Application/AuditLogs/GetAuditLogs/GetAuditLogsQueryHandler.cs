using Choco.Application.Abstractions.Messaging;
using Choco.Contract.Abstractions;
using Choco.Contract.AuditLogs;
using Choco.Domain.AuditLogs;
using Choco.Domain.Users;
using Choco.SharedKernel.Results;

namespace Choco.Application.AuditLogs.GetAuditLogs;

internal sealed class GetAuditLogsQueryHandler(
    IAuditLogRepository auditLogRepository,
    IUserRepository userRepository) : IQueryHandler<GetAuditLogsQuery, OffsetPageResponse<AuditLogResponse>>
{
    public async Task<Result<OffsetPageResponse<AuditLogResponse>>> Handle(
        GetAuditLogsQuery query, CancellationToken cancellationToken)
    {
        AuditLogFilter filter = new(
            query.ActorId,
            AuditLogEnums.TryParse(query.Action, out AuditAction action) ? action : null,
            AuditLogEnums.TryParse(query.TargetType, out AuditTargetType targetType) ? targetType : null,
            query.TargetId,
            query.AffectedUserId,
            query.From,
            query.To);
        long skip = (long)(query.Page - 1) * query.PageSize;

        int totalCount = await auditLogRepository.CountForReadAsync(filter, cancellationToken);
        IReadOnlyList<AuditLogEntry> page = skip >= totalCount
            ? []
            : await auditLogRepository.ListForReadAsync(filter, (int)skip, query.PageSize, cancellationToken);

        List<Guid> userIds = page
            .SelectMany(entry => new[] { entry.ActorId, entry.AffectedUserId })
            .OfType<Guid>()
            .Distinct()
            .ToList();
        IReadOnlyList<User> users = userIds.Count == 0
            ? []
            : await userRepository.ListByIdsIncludingDeletedForReadAsync(userIds, cancellationToken);
        Dictionary<Guid, string> usernames = users.ToDictionary(user => user.Id, user => user.Username);

        List<AuditLogResponse> items = page
            .Select(entry => new AuditLogResponse(
                entry.Id,
                entry.OccurredAt,
                entry.ActorType.ToString(),
                ToUser(entry.ActorId, usernames),
                entry.ActorRoles,
                entry.ClientId,
                entry.Action.ToString(),
                entry.TargetType.ToString(),
                entry.TargetId,
                ToUser(entry.AffectedUserId, usernames),
                entry.Detail,
                entry.TraceId))
            .ToList();

        return Result<OffsetPageResponse<AuditLogResponse>>.Success(
            new OffsetPageResponse<AuditLogResponse>(items, query.Page, query.PageSize, totalCount));
    }

    private static AuditLogUserResponse? ToUser(Guid? userId, Dictionary<Guid, string> usernames)
    {
        return userId is { } id ? new AuditLogUserResponse(id, usernames.GetValueOrDefault(id)) : null;
    }
}
