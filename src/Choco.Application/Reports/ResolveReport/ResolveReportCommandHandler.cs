using Choco.Application.Abstractions.Authentication;
using Choco.Application.Abstractions.Messaging;
using Choco.Contract.Reports;
using Choco.Domain.Primitives.Persistence;
using Choco.Domain.Reports;
using Choco.SharedKernel.Results;

namespace Choco.Application.Reports.ResolveReport;

internal sealed class ResolveReportCommandHandler(
    ICurrentUser currentUser,
    IReportRepository reportRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<ResolveReportCommand>
{
    public async Task<Result> Handle(ResolveReportCommand command, CancellationToken cancellationToken)
    {
        Result<Guid> caller = await currentUser.GetIdAsync(cancellationToken);
        if (caller.IsFailure)
        {
            return caller;
        }

        Report? report = await reportRepository.GetByIdAsync(command.ReportId, cancellationToken);
        if (report is null)
        {
            return Result.Failure(ReportErrors.NotFound);
        }

        ReportEnums.TryParse(command.Outcome, out ReportOutcome outcome);

        report.Resolve(outcome, caller.Value, timeProvider.GetUtcNow());

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
