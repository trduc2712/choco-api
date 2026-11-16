using System.Collections.Frozen;

namespace Choco.Domain.Roles;

public static class RoleIcons
{
    public const string User = "user";
    public const string Shield = "shield";
    public const string Crown = "crown";

    public static readonly IReadOnlySet<string> All =
        new[] { User, Shield, Crown, "star", "flag", "eye", "key", "wrench" }.ToFrozenSet(StringComparer.Ordinal);
}
