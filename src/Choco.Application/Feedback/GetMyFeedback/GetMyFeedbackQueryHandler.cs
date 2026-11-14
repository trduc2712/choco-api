using Choco.Application.Abstractions.Authentication;
using Choco.Application.Abstractions.Messaging;
using Choco.Contract.Abstractions;
using Choco.Contract.Feedback;
using Choco.Domain.FeedbackEntries;
using Choco.SharedKernel.Results;

namespace Choco.Application.Feedback.GetMyFeedback;

internal sealed class GetMyFeedbackQueryHandler(ICurrentUser currentUser, IFeedbackEntryRepository feedbackRepository)
    : IQueryHandler<GetMyFeedbackQuery, PageResponse<MyFeedbackResponse>>
{
    public async Task<Result<PageResponse<MyFeedbackResponse>>> Handle(
        GetMyFeedbackQuery query, CancellationToken cancellationToken)
    {
        Result<Guid> caller = await currentUser.GetIdAsync(cancellationToken);
        if (caller.IsFailure)
        {
            return Result<PageResponse<MyFeedbackResponse>>.Failure(caller.Error);
        }

        (DateTimeOffset CreatedAt, Guid Id)? after =
            FeedbackCursor.TryDecode(query.Cursor, out FeedbackCursor? position)
                ? (position!.CreatedAt, position.FeedbackId)
                : null;

        IReadOnlyList<FeedbackEntry> entries = await feedbackRepository.ListByAuthorForReadAsync(
            caller.Value, after, query.Limit + 1, cancellationToken);

        List<FeedbackEntry> page = entries.Take(query.Limit).ToList();
        string? nextCursor = entries.Count > query.Limit
            ? new FeedbackCursor(page[^1].CreatedAt, page[^1].Id).Encode()
            : null;

        List<MyFeedbackResponse> items = page.Select(entry => entry.ToMyResponse()).ToList();

        return Result<PageResponse<MyFeedbackResponse>>.Success(new PageResponse<MyFeedbackResponse>(items, nextCursor));
    }
}
