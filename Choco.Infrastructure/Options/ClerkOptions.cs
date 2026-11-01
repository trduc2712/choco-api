using System.ComponentModel.DataAnnotations;

namespace Choco.Infrastructure.Options;

internal sealed class ClerkOptions
{
    public const string SectionName = "Clerk";

    [Required(ErrorMessage = $"Missing '{SectionName}:{nameof(SecretKey)}'.")]
    public string SecretKey { get; set; } = string.Empty;

    public Uri BackendApiUrl { get; set; } = new("https://api.clerk.com/v1/");
}
