using Choco.Application.Abstractions.Messaging;
using Choco.Application.Content;
using Choco.Contract.Abstractions;
using Choco.Contract.Feedback;
using Choco.Domain.FeedbackEntries;
using Choco.Domain.Users;
using Choco.SharedKernel.Results;

namespace Choco.Application.Feedback.GetFeedback;

internal sealed class GetFeedbackQueryHandler(
    IFeedbackEntryRepository feedbackRepository,
    IUserRepository userRepository) : IQueryHandler<GetFeedbackQuery, OffsetPageResponse<FeedbackSummaryResponse>>
{
    public async Task<Result<OffsetPageResponse<FeedbackSummaryResponse>>> Handle(
        GetFeedbackQuery query, CancellationToken cancellationToken)
    {
        FeedbackStatus? status = FeedbackWire.TryParse(query.Status, out FeedbackStatus parsedStatus)
            ? parsedStatus
            : null;
        FeedbackCategory? category = FeedbackWire.TryParse(query.Category, out FeedbackCategory parsedCategory)
            ? parsedCategory
            : null;
        string[] terms = SearchTerms.Split(query.Q, GetFeedbackQuery.MaxSearchTerms);
        string? author = string.IsNullOrWhiteSpace(query.Author) ? null : query.Author.Trim();
        var filter = new FeedbackFilter(status, category, terms, author);
        long skip = (long)(query.Page - 1) * query.PageSize;

        int totalCount = await feedbackRepository.CountForReadAsync(filter, cancellationToken);
        IReadOnlyList<FeedbackEntry> entries = skip >= totalCount
            ? []
            : await feedbackRepository.ListForReadAsync(filter, (int)skip, query.PageSize, cancellationToken);

        IReadOnlyList<FeedbackSummaryResponse> items = await entries.ToSummariesAsync(userRepository, cancellationToken);

        return Result<OffsetPageResponse<FeedbackSummaryResponse>>.Success(
            new OffsetPageResponse<FeedbackSummaryResponse>(items, query.Page, query.PageSize, totalCount));
    }
}
