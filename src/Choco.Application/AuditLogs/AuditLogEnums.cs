namespace Choco.Application.AuditLogs;

internal static class AuditLogEnums
{
    public static bool TryParse<TEnum>(string? value, out TEnum result) where TEnum : struct, Enum
    {
        result = default;

        return value is not null
            && Enum.GetNames<TEnum>().Contains(value, StringComparer.Ordinal)
            && Enum.TryParse(value, out result);
    }

    public static string Options<TEnum>() where TEnum : struct, Enum
    {
        return string.Join(", ", Enum.GetNames<TEnum>());
    }
}
