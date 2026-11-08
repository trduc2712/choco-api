using System.Buffers.Text;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

using Choco.Domain.Users;

namespace Choco.Application.Users;

internal static class UserSearchCursor
{
    private const int MaxEncodedLength = 512;

    public static string Encode(UserSearchMatch match, string text)
    {
        string value = string.Create(
            CultureInfo.InvariantCulture,
            $"{match.Rank}:{match.Id:N}:{Hash(text)}:{match.Username}");
        return Base64Url.EncodeToString(Encoding.UTF8.GetBytes(value));
    }

    public static bool TryDecode(
        string? encoded, string text, out (int Rank, string Username, Guid Id) position)
    {
        position = default;
        if (string.IsNullOrEmpty(encoded) || encoded.Length > MaxEncodedLength || text.Length == 0)
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

        string[] parts = value.Split(':', 4);
        if (parts.Length != 4
            || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out int rank)
            || rank > 3
            || !Guid.TryParseExact(parts[1], "N", out Guid id)
            || parts[2] != Hash(text)
            || parts[3].Length is 0 or > User.UsernameMaxLength
            || parts[3].Contains('\0'))
        {
            return false;
        }

        position = (rank, parts[3], id);
        return true;
    }

    private static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}
