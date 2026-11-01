using System.ComponentModel.DataAnnotations;

namespace Choco.Presentation.Options;

internal sealed class ClerkOptions
{
    public const string SectionName = "Clerk";

    [Required(ErrorMessage = $"Missing '{SectionName}:{nameof(Authority)}'.")]
    [Url(ErrorMessage = $"'{SectionName}:{nameof(Authority)}' must be a valid URL.")]
    public string Authority { get; set; } = string.Empty;

    [MinLength(1, ErrorMessage = $"'{SectionName}:{nameof(AuthorizedParties)}' must contain at least one origin.")]
    public List<string> AuthorizedParties { get; set; } = [];

    public string? PublishableKey { get; set; }

    [Required(ErrorMessage = $"Missing '{SectionName}:{nameof(WebhookSigningSecret)}'.")]
    public string WebhookSigningSecret { get; set; } = string.Empty;
}
