using Choco.Application.Abstractions.Messaging;
using Choco.Contract.Abstractions;
using Choco.Contract.Reports;
using Choco.Domain.Reports;
using Choco.Domain.Users;
using Choco.SharedKernel.Results;

namespace Choco.Application.Reports.GetReports;

internal sealed class GetReportsQueryHandler(IReportRepository reportRepository, IUserRepository userRepository)
    : IQueryHandler<GetReportsQuery, OffsetPageResponse<ReportResponse>>
{
    public async Task<Result<OffsetPageResponse<ReportResponse>>> Handle(
        GetReportsQuery query, CancellationToken cancellationToken)
    {
        long skip = (long)(query.Page - 1) * query.PageSize;

        int totalCount = await reportRepository.CountOpenAsync(cancellationToken);
        IReadOnlyList<Report> reports = skip >= totalCount
            ? []
            : await reportRepository.ListOpenForReadAsync((int)skip, query.PageSize, cancellationToken);

        IReadOnlyList<User> reporters = await userRepository.ListByIdsForReadAsync(
            reports.Select(report => report.ReporterId).Distinct().ToList(), cancellationToken);
        Dictionary<Guid, string> usernames = reporters.ToDictionary(user => user.Id, user => user.Username);

        List<ReportResponse> items = reports
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

        return Result<OffsetPageResponse<ReportResponse>>.Success(
            new OffsetPageResponse<ReportResponse>(items, query.Page, query.PageSize, totalCount));
    }
}
