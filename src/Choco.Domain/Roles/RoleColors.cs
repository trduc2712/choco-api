using System.Collections.Frozen;

namespace Choco.Domain.Roles;

public static class RoleColors
{
    public const string Gray = "gray";
    public const string Purple = "purple";
    public const string Teal = "teal";

    public static readonly IReadOnlySet<string> All =
        new[] { Purple, "coral", Teal, "blue", "pink", "amber", Gray }.ToFrozenSet(StringComparer.Ordinal);
}
