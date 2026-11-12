using Choco.Application.Abstractions.Authorization;
using Choco.Application.Abstractions.Messaging;
using Choco.Contract.AdminCounts;
using Choco.Domain.FeedbackEntries;
using Choco.Domain.Reports;
using Choco.Domain.Roles;
using Choco.SharedKernel.Results;

namespace Choco.Application.AdminCounts.GetAdminCounts;

internal sealed class GetAdminCountsQueryHandler(
    IPermissionChecker permissionChecker,
    IFeedbackEntryRepository feedbackRepository,
    IReportRepository reportRepository) : IQueryHandler<GetAdminCountsQuery, AdminCountsResponse>
{
    public async Task<Result<AdminCountsResponse>> Handle(
        GetAdminCountsQuery query, CancellationToken cancellationToken)
    {
        Result<bool> canReadFeedback = await permissionChecker.IsGrantedToCurrentUserAsync(
            Permission.ReadAnyFeedback, PermissionScope.Platform, cancellationToken);
        if (canReadFeedback.IsFailure)
        {
            return Result<AdminCountsResponse>.Failure(canReadFeedback.Error);
        }

        Result<bool> canHandleReports = await permissionChecker.IsGrantedToCurrentUserAsync(
            Permission.HandleReports, PermissionScope.Platform, cancellationToken);
        if (canHandleReports.IsFailure)
        {
            return Result<AdminCountsResponse>.Failure(canHandleReports.Error);
        }

        if (!canReadFeedback.Value && !canHandleReports.Value)
        {
            return Result<AdminCountsResponse>.Failure(PermissionErrors.Denied);
        }

        int? newFeedback = canReadFeedback.Value
            ? await feedbackRepository.CountByStatusAsync(FeedbackStatus.New, cancellationToken)
            : null;
        int? openReports = canHandleReports.Value
            ? await reportRepository.CountQueueAsync(new ReportQueueFilter(null, null), cancellationToken)
            : null;

        return Result<AdminCountsResponse>.Success(new AdminCountsResponse(newFeedback, openReports));
    }
}
