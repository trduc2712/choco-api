using Choco.Application.Abstractions.Messaging;
using Choco.Contract.Abstractions;
using Choco.Contract.AuditLogs;
using Choco.Domain.AuditLogs;
using Choco.Domain.Users;
using Choco.SharedKernel.Results;

namespace Choco.Application.AuditLogs.GetAuditLogs;

internal sealed class GetAuditLogsQueryHandler(
    IAuditLogRepository auditLogRepository,
    IUserRepository userRepository) : IQueryHandler<GetAuditLogsQuery, PageResponse<AuditLogResponse>>
{
    public async Task<Result<PageResponse<AuditLogResponse>>> Handle(
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
        (DateTimeOffset OccurredAt, Guid Id)? after =
            AuditLogCursor.TryDecode(query.Cursor, out AuditLogCursor? position)
                ? (position!.OccurredAt, position.EntryId)
                : null;

        IReadOnlyList<AuditLogEntry> entries = await auditLogRepository.ListForReadAsync(
            filter, after, query.Limit + 1, cancellationToken);

        List<AuditLogEntry> page = entries.Take(query.Limit).ToList();
        string? nextCursor = entries.Count > query.Limit
            ? new AuditLogCursor(page[^1].OccurredAt, page[^1].Id).Encode()
            : null;

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

        return Result<PageResponse<AuditLogResponse>>.Success(new PageResponse<AuditLogResponse>(items, nextCursor));
    }

    private static AuditLogUserResponse? ToUser(Guid? userId, Dictionary<Guid, string> usernames)
    {
        return userId is { } id ? new AuditLogUserResponse(id, usernames.GetValueOrDefault(id)) : null;
    }
}
