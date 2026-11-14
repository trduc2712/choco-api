using Choco.Application.Abstractions.Messaging;
using Choco.Contract.Abstractions;
using Choco.Contract.Reports;
using Choco.Domain.Reports;
using Choco.Domain.Users;
using Choco.SharedKernel.Results;

namespace Choco.Application.Reports.GetAdminReports;

internal sealed class GetAdminReportsQueryHandler(
    IReportRepository reportRepository,
    IUserRepository userRepository)
    : IQueryHandler<GetAdminReportsQuery, OffsetPageResponse<AdminReportResponse>>
{
    public async Task<Result<OffsetPageResponse<AdminReportResponse>>> Handle(
        GetAdminReportsQuery query, CancellationToken cancellationToken)
    {
        ReportListFilter filter = new(
            ReportEnums.TryParse(query.TargetType, out ReportTargetType targetType) ? targetType : null,
            query.TargetId,
            ReportEnums.TryParse(query.Status, out ReportStatus status) ? status : null,
            ReportEnums.TryParse(query.Reason, out ReportReason reason) ? reason : null,
            ReportEnums.TryParse(query.Outcome, out ReportOutcome outcome) ? outcome : null,
            ReportSearches.Create(query.Q, GetAdminReportsQuery.MaxSearchWords),
            query.From,
            query.To);
        long skip = (long)(query.Page - 1) * query.PageSize;

        int totalCount = await reportRepository.CountForReadAsync(filter, cancellationToken);
        IReadOnlyList<Report> reports = skip >= totalCount
            ? []
            : await reportRepository.ListForReadAsync(filter, (int)skip, query.PageSize, cancellationToken);

        List<Guid> userIds = reports
            .SelectMany(report => new[] { report.ReporterId, report.ResolvedById })
            .OfType<Guid>()
            .Distinct()
            .ToList();
        Dictionary<Guid, string> usernames = userIds.Count == 0
            ? []
            : (await userRepository.ListByIdsForReadAsync(userIds, cancellationToken))
                .ToDictionary(user => user.Id, user => user.Username);

        List<AdminReportResponse> items = reports
            .Select(report => new AdminReportResponse(
                report.Id,
                report.TargetType.ToString(),
                (report.TargetPostId ?? report.TargetUserId)!.Value,
                report.Reason.ToString(),
                report.Details,
                ToUser(report.ReporterId, usernames)!,
                report.CreatedAt,
                report.Status.ToString(),
                report.Outcome?.ToString(),
                ToUser(report.ResolvedById, usernames),
                report.ResolvedAt))
            .ToList();

        return Result<OffsetPageResponse<AdminReportResponse>>.Success(
            new OffsetPageResponse<AdminReportResponse>(items, query.Page, query.PageSize, totalCount));
    }

    private static ReportUserResponse? ToUser(Guid? userId, Dictionary<Guid, string> usernames)
    {
        return userId is { } id ? new ReportUserResponse(id, usernames.GetValueOrDefault(id)) : null;
    }
}
