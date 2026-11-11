using Choco.Application.Abstractions.Authentication;
using Choco.Application.Abstractions.Messaging;
using Choco.Contract.Reports;
using Choco.Domain.Primitives.Persistence;
using Choco.Domain.Reports;
using Choco.Domain.Users;
using Choco.SharedKernel.Results;

namespace Choco.Application.Reports.ReportUser;

internal sealed class ReportUserCommandHandler(
    ICurrentUser currentUser,
    IUserRepository userRepository,
    IReportRepository reportRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<ReportUserCommand>
{
    public async Task<Result> Handle(ReportUserCommand command, CancellationToken cancellationToken)
    {
        Result<Guid> caller = await currentUser.GetIdAsync(cancellationToken);
        if (caller.IsFailure)
        {
            return caller;
        }

        User? target = await userRepository.GetByIdForReadAsync(command.UserId, cancellationToken);
        if (target is null)
        {
            return Result.Failure(UserErrors.NotFound);
        }

        ReportEnums.TryParse(command.Reason, out ReportReason reason);

        Result<Report> report = Report.CreateForUser(
            caller.Value, target.Id, reason, command.Details, timeProvider.GetUtcNow());
        if (report.IsFailure)
        {
            return report;
        }

        await unitOfWork.InTransactionAsync(async () =>
        {
            if (await reportRepository.AddIfNoOpenAsync(report.Value, cancellationToken))
            {
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }

            return true;
        }, cancellationToken);

        return Result.Success();
    }
}
