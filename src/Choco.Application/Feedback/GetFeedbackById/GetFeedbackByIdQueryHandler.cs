using Choco.Application.Abstractions.Messaging;
using Choco.Contract.Feedback;
using Choco.Domain.FeedbackEntries;
using Choco.Domain.Users;
using Choco.SharedKernel.Results;

namespace Choco.Application.Feedback.GetFeedbackById;

internal sealed class GetFeedbackByIdQueryHandler(
    IFeedbackEntryRepository feedbackRepository,
    IUserRepository userRepository) : IQueryHandler<GetFeedbackByIdQuery, FeedbackResponse>
{
    public async Task<Result<FeedbackResponse>> Handle(GetFeedbackByIdQuery query, CancellationToken cancellationToken)
    {
        FeedbackEntry? entry = await feedbackRepository.GetByIdForReadAsync(query.Id, cancellationToken);
        if (entry is null)
        {
            return Result<FeedbackResponse>.Failure(FeedbackErrors.NotFound);
        }

        User? author = await userRepository.GetByIdForReadAsync(entry.AuthorId, cancellationToken);

        return Result<FeedbackResponse>.Success(entry.ToResponse(author));
    }
}
