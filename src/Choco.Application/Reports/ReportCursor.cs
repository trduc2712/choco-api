using System.Buffers.Text;
using System.Globalization;
using System.Text;

namespace Choco.Application.Reports;

internal sealed record ReportCursor(DateTimeOffset CreatedAt, Guid ReportId)
{
    private const char Separator = '_';

    public string Encode()
    {
        string value = string.Create(
            CultureInfo.InvariantCulture,
            $"{CreatedAt.UtcTicks}{Separator}{ReportId:N}");

        return Base64Url.EncodeToString(Encoding.UTF8.GetBytes(value));
    }

    public static bool TryDecode(string? encoded, out ReportCursor? cursor)
    {
        cursor = null;

        if (string.IsNullOrEmpty(encoded))
        {
            return false;
        }

        byte[] bytes;

        try
        {
            bytes = Base64Url.DecodeFromChars(encoded);
        }
        catch (FormatException)
        {
            return false;
        }

        string[] parts = Encoding.UTF8.GetString(bytes).Split(Separator);

        if (parts.Length != 2
            || !long.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out long ticks)
            || ticks > DateTimeOffset.MaxValue.UtcTicks
            || !Guid.TryParseExact(parts[1], "N", out Guid reportId))
        {
            return false;
        }

        cursor = new ReportCursor(new DateTimeOffset(ticks, TimeSpan.Zero), reportId);

        return true;
    }
}
