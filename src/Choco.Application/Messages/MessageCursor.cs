using System.Buffers.Text;
using System.Globalization;
using System.Text;

namespace Choco.Application.Messages;

internal sealed record MessageCursor(long Sequence, bool IsCatchUp)
{
    public string Encode()
    {
        string value = string.Create(CultureInfo.InvariantCulture, $"{(IsCatchUp ? 'a' : 'b')}{Sequence}");
        return Base64Url.EncodeToString(Encoding.UTF8.GetBytes(value));
    }

    public static bool TryDecode(string? encoded, out MessageCursor? cursor)
    {
        cursor = null;
        if (string.IsNullOrEmpty(encoded))
        {
            return false;
        }

        string value;
        try
        {
            value = Encoding.UTF8.GetString(Base64Url.DecodeFromChars(encoded));
        }
        catch (FormatException)
        {
            return false;
        }

        if (value.Length < 2 || value[0] is not ('a' or 'b')
            || !long.TryParse(value.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out long sequence)
            || sequence == 0)
        {
            return false;
        }

        cursor = new MessageCursor(sequence, value[0] == 'a');
        return true;
    }
}
