using System.ComponentModel.DataAnnotations;

namespace Choco.Presentation.ForwardedHeaders;

internal sealed class ForwardedHeadersSettings
{
    public const string SectionName = "ForwardedHeaders";

    public bool Enabled { get; set; }

    [Range(1, 10, ErrorMessage = $"'{SectionName}:{nameof(ForwardLimit)}' must be between 1 and 10.")]
    public int ForwardLimit { get; set; } = 1;

    public bool TrustAnyProxy { get; set; }

    public List<string> KnownProxies { get; set; } = [];

    public List<string> KnownNetworks { get; set; } = [];
}
