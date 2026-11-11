using Choco.Application.Abstractions.Messaging;
using Choco.Contract.Abstractions;
using Choco.Contract.Feedback;
using Choco.Domain.FeedbackEntries;
using Choco.Domain.Users;
using Choco.SharedKernel.Results;

namespace Choco.Application.Feedback.GetFeedback;

internal sealed class GetFeedbackQueryHandler(
    IFeedbackEntryRepository feedbackRepository,
    IUserRepository userRepository) : IQueryHandler<GetFeedbackQuery, PageResponse<FeedbackResponse>>
{
    public async Task<Result<PageResponse<FeedbackResponse>>> Handle(
        GetFeedbackQuery query, CancellationToken cancellationToken)
    {
        FeedbackStatus? status = FeedbackWire.TryParse(query.Status, out FeedbackStatus parsedStatus)
            ? parsedStatus
            : null;
        FeedbackCategory? category = FeedbackWire.TryParse(query.Category, out FeedbackCategory parsedCategory)
            ? parsedCategory
            : null;
        (DateTimeOffset CreatedAt, Guid Id)? after =
            FeedbackCursor.TryDecode(query.Cursor, out FeedbackCursor? position)
                ? (position!.CreatedAt, position.FeedbackId)
                : null;

        IReadOnlyList<FeedbackEntry> entries = await feedbackRepository.ListForReadAsync(
            status, category, after, query.Limit + 1, cancellationToken);

        List<FeedbackEntry> page = entries.Take(query.Limit).ToList();
        string? nextCursor = entries.Count > query.Limit
            ? new FeedbackCursor(page[^1].CreatedAt, page[^1].Id).Encode()
            : null;

        IReadOnlyList<FeedbackResponse> items = await page.ToResponsesAsync(userRepository, cancellationToken);

        return Result<PageResponse<FeedbackResponse>>.Success(new PageResponse<FeedbackResponse>(items, nextCursor));
    }
}
