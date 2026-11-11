using System.ComponentModel.DataAnnotations;

namespace Choco.Presentation.RateLimiting;

internal sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    [Range(1, int.MaxValue, ErrorMessage = $"'{SectionName}:{nameof(TokenLimit)}' must be at least 1.")]
    public int TokenLimit { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = $"'{SectionName}:{nameof(TokensPerPeriod)}' must be at least 1.")]
    public int TokensPerPeriod { get; set; }

    [Range(typeof(TimeSpan), "00:00:01", "1.00:00:00",
        ErrorMessage = $"'{SectionName}:{nameof(ReplenishmentPeriod)}' must be between 1 second and 1 day.")]
    public TimeSpan ReplenishmentPeriod { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = $"'{SectionName}:{nameof(FeedbackPerHour)}' must be at least 1.")]
    public int FeedbackPerHour { get; set; } = 5;

    [Range(1, int.MaxValue, ErrorMessage = $"'{SectionName}:{nameof(FeedbackPerDay)}' must be at least 1.")]
    public int FeedbackPerDay { get; set; } = 20;
}
