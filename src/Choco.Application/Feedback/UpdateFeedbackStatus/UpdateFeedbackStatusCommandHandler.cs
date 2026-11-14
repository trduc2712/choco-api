using Choco.Application.Abstractions.Auditing;
using Choco.Application.Abstractions.Messaging;
using Choco.Contract.Feedback;
using Choco.Domain.AuditLogs;
using Choco.Domain.FeedbackEntries;
using Choco.Domain.Primitives.Persistence;
using Choco.Domain.Users;
using Choco.SharedKernel.Results;

namespace Choco.Application.Feedback.UpdateFeedbackStatus;

internal sealed class UpdateFeedbackStatusCommandHandler(
    IFeedbackEntryRepository feedbackRepository,
    IUserRepository userRepository,
    IUnitOfWork unitOfWork,
    AuditChanges auditChanges) : ICommandHandler<UpdateFeedbackStatusCommand, FeedbackResponse>
{
    public async Task<Result<FeedbackResponse>> Handle(
        UpdateFeedbackStatusCommand command, CancellationToken cancellationToken)
    {
        FeedbackEntry? entry = await feedbackRepository.GetByIdAsync(command.Id, cancellationToken);
        if (entry is null)
        {
            return Result<FeedbackResponse>.Failure(FeedbackErrors.NotFound);
        }

        FeedbackWire.TryParse(command.Status, out FeedbackStatus status);

        FeedbackStatus before = entry.Status;
        Result change = entry.ChangeStatus(status);
        if (change.IsFailure)
        {
            return Result<FeedbackResponse>.Failure(change.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        User? author = await userRepository.GetByIdForReadAsync(entry.AuthorId, cancellationToken);

        FeedbackResponse response = entry.ToResponse(author);
        return auditChanges.Record(Result<FeedbackResponse>.Success(response), before == entry.Status
            ? []
            : [new AuditChange("status", FeedbackWire.ToWire(before), response.Status)]);
    }
}
