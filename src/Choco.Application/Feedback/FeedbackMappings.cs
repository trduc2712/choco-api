using Choco.Contract.Feedback;
using Choco.Domain.FeedbackEntries;
using Choco.Domain.Users;

namespace Choco.Application.Feedback;

internal static class FeedbackMappings
{
    public static FeedbackContextDto ToDto(this FeedbackContext context)
    {
        return new FeedbackContextDto(
            context.Path,
            FeedbackWire.ToWire(context.Locale),
            FeedbackWire.ToWire(context.Theme),
            context.UserAgent,
            new FeedbackViewportDto(context.ViewportWidth, context.ViewportHeight),
            context.AppVersion);
    }

    public static FeedbackContext ToDomain(this CreateFeedbackContextDto dto, string userAgent)
    {
        FeedbackWire.TryParse(dto.Locale, out FeedbackLocale locale);
        FeedbackWire.TryParse(dto.Theme, out FeedbackTheme theme);

        return new FeedbackContext(
            dto.Path,
            locale,
            theme,
            userAgent.Length > FeedbackContext.UserAgentMaxLength ? userAgent[..FeedbackContext.UserAgentMaxLength] : userAgent,
            dto.Viewport.Width,
            dto.Viewport.Height,
            dto.AppVersion);
    }

    public static MyFeedbackResponse ToMyResponse(this FeedbackEntry entry)
    {
        return new MyFeedbackResponse(
            entry.Id,
            FeedbackWire.ToWire(entry.Category),
            entry.Message,
            FeedbackWire.ToWire(entry.Status),
            entry.CreatedAt);
    }

    public static FeedbackSummaryResponse ToSummary(this FeedbackEntry entry, User? author)
    {
        return new FeedbackSummaryResponse(
            entry.Id,
            author is null ? null : new FeedbackAuthorResponse(author.Id, author.Username, author.DisplayName),
            FeedbackWire.ToWire(entry.Category),
            Preview(entry.Message),
            FeedbackWire.ToWire(entry.Status),
            entry.CreatedAt);
    }

    private static string Preview(string message)
    {
        if (message.Length <= FeedbackSummaryResponse.MessagePreviewMaxLength)
        {
            return message;
        }

        int length = FeedbackSummaryResponse.MessagePreviewMaxLength;
        if (char.IsHighSurrogate(message[length - 1]))
        {
            length--;
        }

        return message[..length].TrimEnd() + "…";
    }

    public static async Task<IReadOnlyList<FeedbackSummaryResponse>> ToSummariesAsync(
        this IReadOnlyList<FeedbackEntry> entries,
        IUserRepository userRepository,
        CancellationToken cancellationToken)
    {
        if (entries.Count == 0)
        {
            return [];
        }

        IReadOnlyList<User> authors = await userRepository.ListByIdsForReadAsync(
            entries.Select(entry => entry.AuthorId).Distinct().ToList(), cancellationToken);
        Dictionary<Guid, User> authorsById = authors.ToDictionary(author => author.Id);

        return entries
            .Select(entry => entry.ToSummary(authorsById.GetValueOrDefault(entry.AuthorId)))
            .ToList();
    }

    public static FeedbackResponse ToResponse(this FeedbackEntry entry, User? author)
    {
        return new FeedbackResponse(
            entry.Id,
            author is null ? null : new FeedbackAuthorResponse(author.Id, author.Username, author.DisplayName),
            FeedbackWire.ToWire(entry.Category),
            entry.Message,
            entry.Context.ToDto(),
            FeedbackWire.ToWire(entry.Status),
            entry.CreatedAt,
            entry.ModifiedAt);
    }
}
