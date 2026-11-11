using Choco.Application.Abstractions.Messaging;
using Choco.Contract.Abstractions;
using Choco.Contract.Feedback;
using Choco.Domain.FeedbackEntries;
using Choco.Domain.Users;
using Choco.SharedKernel.Results;

namespace Choco.Application.Feedback.GetFeedback;

internal sealed class GetFeedbackQueryHandler(
    IFeedbackEntryRepository feedbackRepository,
    IUserRepository userRepository) : IQueryHandler<GetFeedbackQuery, OffsetPageResponse<FeedbackResponse>>
{
    public async Task<Result<OffsetPageResponse<FeedbackResponse>>> Handle(
        GetFeedbackQuery query, CancellationToken cancellationToken)
    {
        FeedbackStatus? status = FeedbackWire.TryParse(query.Status, out FeedbackStatus parsedStatus)
            ? parsedStatus
            : null;
        FeedbackCategory? category = FeedbackWire.TryParse(query.Category, out FeedbackCategory parsedCategory)
            ? parsedCategory
            : null;
        string[] terms = (query.Q ?? string.Empty)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Take(GetFeedbackQuery.MaxSearchTerms)
            .ToArray();
        string? author = string.IsNullOrWhiteSpace(query.Author) ? null : query.Author.Trim();
        var filter = new FeedbackFilter(status, category, terms, author);
        long skip = (long)(query.Page - 1) * query.PageSize;

        int totalCount = await feedbackRepository.CountForReadAsync(filter, cancellationToken);
        IReadOnlyList<FeedbackEntry> entries = skip >= totalCount
            ? []
            : await feedbackRepository.ListForReadAsync(filter, (int)skip, query.PageSize, cancellationToken);

        IReadOnlyList<FeedbackResponse> items = await entries.ToResponsesAsync(userRepository, cancellationToken);

        return Result<OffsetPageResponse<FeedbackResponse>>.Success(
            new OffsetPageResponse<FeedbackResponse>(items, query.Page, query.PageSize, totalCount));
    }
}
