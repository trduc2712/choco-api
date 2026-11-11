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

    public static FeedbackContext ToDomain(this FeedbackContextDto dto)
    {
        FeedbackWire.TryParse(dto.Locale, out FeedbackLocale locale);
        FeedbackWire.TryParse(dto.Theme, out FeedbackTheme theme);

        return new FeedbackContext(
            dto.Path,
            locale,
            theme,
            dto.UserAgent,
            dto.Viewport.Width,
            dto.Viewport.Height,
            dto.AppVersion);
    }

    public static async Task<IReadOnlyList<FeedbackResponse>> ToResponsesAsync(
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
            .Select(entry => entry.ToResponse(authorsById.GetValueOrDefault(entry.AuthorId)))
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
