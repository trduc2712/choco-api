using Choco.Application.Abstractions.Messaging;
using Choco.Contract.Abstractions;
using Choco.Contract.Reports;
using Choco.Domain.Reports;
using Choco.Domain.Users;
using Choco.SharedKernel.Results;

namespace Choco.Application.Reports.GetReports;

internal sealed class GetReportsQueryHandler(IReportRepository reportRepository, IUserRepository userRepository)
    : IQueryHandler<GetReportsQuery, PageResponse<ReportResponse>>
{
    public async Task<Result<PageResponse<ReportResponse>>> Handle(
        GetReportsQuery query, CancellationToken cancellationToken)
    {
        (DateTimeOffset CreatedAt, Guid Id)? after =
            ReportCursor.TryDecode(query.Cursor, out ReportCursor? position)
                ? (position!.CreatedAt, position.ReportId)
                : null;

        IReadOnlyList<Report> reports = await reportRepository.ListOpenForReadAsync(
            after, query.Limit + 1, cancellationToken);

        List<Report> page = reports.Take(query.Limit).ToList();
        string? nextCursor = reports.Count > query.Limit
            ? new ReportCursor(page[^1].CreatedAt, page[^1].Id).Encode()
            : null;

        IReadOnlyList<User> reporters = await userRepository.ListByIdsForReadAsync(
            page.Select(report => report.ReporterId).Distinct().ToList(), cancellationToken);
        Dictionary<Guid, string> usernames = reporters.ToDictionary(user => user.Id, user => user.Username);

        List<ReportResponse> items = page
            .Select(report => new ReportResponse(
                report.Id,
                report.TargetType.ToString(),
                report.TargetPostId,
                report.TargetUserId,
                report.Reason.ToString(),
                report.Details,
                report.ReporterId,
                usernames.GetValueOrDefault(report.ReporterId),
                report.CreatedAt))
            .ToList();

        return Result<PageResponse<ReportResponse>>.Success(new PageResponse<ReportResponse>(items, nextCursor));
    }
}
