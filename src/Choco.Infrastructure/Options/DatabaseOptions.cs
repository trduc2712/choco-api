using System.ComponentModel.DataAnnotations;

namespace Choco.Infrastructure.Options;

internal sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    [Required(ErrorMessage = $"Missing '{SectionName}:{nameof(ConnectionString)}'.")]
    public string ConnectionString { get; set; } = string.Empty;
}
