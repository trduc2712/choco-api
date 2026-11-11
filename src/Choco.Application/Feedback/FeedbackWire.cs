using System.Text;

namespace Choco.Application.Feedback;

internal static class FeedbackWire
{
    public static string ToWire<TEnum>(TEnum value) where TEnum : struct, Enum
    {
        string name = value.ToString();
        StringBuilder builder = new(name.Length + 2);

        for (int index = 0; index < name.Length; index++)
        {
            if (index > 0 && char.IsUpper(name[index]))
            {
                builder.Append('_');
            }

            builder.Append(char.ToLowerInvariant(name[index]));
        }

        return builder.ToString();
    }

    public static bool TryParse<TEnum>(string? value, out TEnum result) where TEnum : struct, Enum
    {
        result = default;

        if (value is null)
        {
            return false;
        }

        foreach (TEnum candidate in Enum.GetValues<TEnum>())
        {
            if (string.Equals(ToWire(candidate), value, StringComparison.Ordinal))
            {
                result = candidate;

                return true;
            }
        }

        return false;
    }

    public static string Options<TEnum>() where TEnum : struct, Enum
    {
        return string.Join(", ", Enum.GetValues<TEnum>().Select(ToWire));
    }
}
