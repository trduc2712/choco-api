using Choco.Domain.Users;

namespace Choco.Application.Users;

internal static class UserSearchText
{
    public static bool IsValid(string? text)
    {
        string trimmed = text?.Trim() ?? string.Empty;
        return trimmed.Length <= User.DisplayNameMaxLength && !trimmed.Contains('\0');
    }
}
