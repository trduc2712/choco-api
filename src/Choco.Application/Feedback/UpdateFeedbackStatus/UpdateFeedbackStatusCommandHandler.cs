using Choco.Application.Abstractions.Messaging;
using Choco.Contract.Feedback;
using Choco.Domain.FeedbackEntries;
using Choco.Domain.Primitives.Persistence;
using Choco.Domain.Users;
using Choco.SharedKernel.Results;

namespace Choco.Application.Feedback.UpdateFeedbackStatus;

internal sealed class UpdateFeedbackStatusCommandHandler(
    IFeedbackEntryRepository feedbackRepository,
    IUserRepository userRepository,
    IUnitOfWork unitOfWork) : ICommandHandler<UpdateFeedbackStatusCommand, FeedbackResponse>
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

        entry.ChangeStatus(status);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        User? author = await userRepository.GetByIdForReadAsync(entry.AuthorId, cancellationToken);

        return Result<FeedbackResponse>.Success(entry.ToResponse(author));
    }
}
