using Choco.Application.Abstractions.Auditing;
using Choco.Application.Abstractions.Authentication;
using Choco.Application.Abstractions.Authorization;
using Choco.Application.Abstractions.Messaging;
using Choco.Application.Users;
using Choco.Contract.Reports;
using Choco.Domain.AuditLogs;
using Choco.Domain.Posts;
using Choco.Domain.Primitives.Persistence;
using Choco.Domain.Reports;
using Choco.Domain.Roles;
using Choco.Domain.Users;
using Choco.SharedKernel.Results;

namespace Choco.Application.Reports.ResolveReport;

internal sealed class ResolveReportCommandHandler(
    ICurrentUser currentUser,
    IPermissionChecker permissionChecker,
    IReportRepository reportRepository,
    IPostRepository postRepository,
    IUserRepository userRepository,
    UserSuspender suspender,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    AuditChanges auditChanges) : ICommandHandler<ResolveReportCommand>
{
    public async Task<Result> Handle(ResolveReportCommand command, CancellationToken cancellationToken)
    {
        Result<Guid> caller = await currentUser.GetIdAsync(cancellationToken);
        if (caller.IsFailure)
        {
            return caller;
        }

        Report? report = await reportRepository.GetByIdForReadAsync(command.ReportId, cancellationToken);
        if (report is null)
        {
            return Result.Failure(ReportErrors.NotFound);
        }

        if (report.Status == ReportStatus.Resolved)
        {
            return Result.Success();
        }

        ReportEnums.TryParse(command.Outcome, out ReportOutcome outcome);

        Result applies = report.CheckOutcomeApplies(outcome);
        if (applies.IsFailure)
        {
            return applies;
        }

        return await unitOfWork.InTransactionAsync(
            () => ResolveAsync(report, outcome, caller.Value, cancellationToken), cancellationToken);
    }

    private async Task<Result> ResolveAsync(
        Report report, ReportOutcome outcome, Guid callerId, CancellationToken cancellationToken)
    {
        Result action = outcome switch
        {
            ReportOutcome.ContentRemoved => await RemoveContentAsync(report, cancellationToken),
            ReportOutcome.UserSuspended => await SuspendUserAsync(report, callerId, cancellationToken),
            _ => Result.Success()
        };

        if (action.IsFailure)
        {
            return action;
        }

        int resolved = await reportRepository.ResolveOpenAboutTheSameTargetAsync(
            report, outcome, callerId, timeProvider.GetUtcNow(), cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return auditChanges.Record(Result.Success(), resolved == 0 ? [] : [new AuditChange("outcome", After: outcome.ToString())]);
    }

    private async Task<Result> RemoveContentAsync(Report report, CancellationToken cancellationToken)
    {
        Result<bool> granted = await permissionChecker.IsGrantedToCurrentUserAsync(
            Permission.DeleteAnyPost, PermissionScope.Platform, cancellationToken);
        if (granted.IsFailure)
        {
            return granted;
        }

        if (!granted.Value)
        {
            return Result.Failure(PermissionErrors.Denied);
        }

        Post? post = await postRepository.GetByIdAsync(report.TargetPostId!.Value, cancellationToken);
        if (post is not null)
        {
            postRepository.Remove(post);
        }

        return Result.Success();
    }

    private async Task<Result> SuspendUserAsync(Report report, Guid callerId, CancellationToken cancellationToken)
    {
        Result<bool> granted = await permissionChecker.IsGrantedToCurrentUserAsync(
            Permission.SuspendUsers, PermissionScope.Platform, cancellationToken);
        if (granted.IsFailure)
        {
            return granted;
        }

        if (!granted.Value)
        {
            return Result.Failure(PermissionErrors.Denied);
        }

        Guid? userId = report.TargetUserId;
        if (userId is null)
        {
            Post? post = await postRepository.GetByIdIncludingDeletedForReadAsync(report.TargetPostId!.Value, cancellationToken);
            userId = post?.AuthorId;
        }

        User? user = userId is { } id ? await userRepository.GetByIdWithRolesAsync(id, cancellationToken) : null;
        if (user is null)
        {
            return Result.Failure(UserErrors.NotFound);
        }

        return user.IsSuspended
            ? Result.Success()
            : await suspender.SuspendAsync(user, callerId, report.SuspensionReason, cancellationToken);
    }
}
