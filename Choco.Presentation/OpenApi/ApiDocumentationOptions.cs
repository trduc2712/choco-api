using System.ComponentModel.DataAnnotations;

namespace Choco.Presentation.OpenApi;

internal sealed class ApiDocumentationOptions
{
    public const string SectionName = "ApiDocumentation";

    public bool Enabled { get; set; }

    [Required(ErrorMessage = $"Missing '{SectionName}:{nameof(Title)}'.")]
    public string Title { get; set; } = "Choco API";
}
