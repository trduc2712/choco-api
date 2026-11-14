using Choco.Application.Abstractions.Authentication;
using Choco.Application.Abstractions.Messaging;
using Choco.Contract.Feedback;
using Choco.Domain.FeedbackEntries;
using Choco.Domain.Primitives.Persistence;
using Choco.SharedKernel.Results;

namespace Choco.Application.Feedback.CreateFeedback;

internal sealed class CreateFeedbackCommandHandler(
    ICurrentUser currentUser,
    IFeedbackEntryRepository feedbackRepository,
    IUnitOfWork unitOfWork) : ICommandHandler<CreateFeedbackCommand, CreatedFeedbackResponse>
{
    public async Task<Result<CreatedFeedbackResponse>> Handle(
        CreateFeedbackCommand command, CancellationToken cancellationToken)
    {
        Result<Guid> caller = await currentUser.GetIdAsync(cancellationToken);
        if (caller.IsFailure)
        {
            return Result<CreatedFeedbackResponse>.Failure(caller.Error);
        }

        FeedbackWire.TryParse(command.Category, out FeedbackCategory category);

        FeedbackEntry entry = FeedbackEntry.Create(caller.Value, category, command.Message, command.Context.ToDomain(command.UserAgent ?? string.Empty));

        feedbackRepository.Add(entry);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<CreatedFeedbackResponse>.Success(new CreatedFeedbackResponse(entry.Id, entry.CreatedAt));
    }
}
