using System.Buffers.Text;
using System.Text;

using Choco.Application.Notifications;

namespace Choco.Application.UnitTests.Notifications;

public sealed class NotificationCursorTests
{
    private static readonly NotificationCursor Cursor = new(
        new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
        new Guid("3c4d5e6f-7a8b-4c9d-8e0f-1a2b3c4d5e6f"));

    [Fact]
    public void Encode_ThenTryDecode_ReturnsSameCursor()
    {
        string encoded = Cursor.Encode();

        bool decoded = NotificationCursor.TryDecode(encoded, out NotificationCursor? cursor);

        decoded.Should().BeTrue();
        cursor.Should().Be(Cursor);
    }

    [Fact]
    public void Encode_ReturnsUrlSafeText()
    {
        string encoded = Cursor.Encode();

        encoded.Should().NotContainAny("+", "/", "=");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [MemberData(nameof(InvalidCursors))]
    public void TryDecode_WithInvalidText_ReturnsFalse(string? encoded)
    {
        bool decoded = NotificationCursor.TryDecode(encoded, out NotificationCursor? cursor);

        decoded.Should().BeFalse();
        cursor.Should().BeNull();
    }

    public static TheoryData<string> InvalidCursors => new()
    {
        "not base64url!",
        Base64UrlOf("639033120000000000"),
        Base64UrlOf("639033120000000000_3c4d5e6f7a8b4c9d8e0f1a2b3c4d5e6f_extra"),
        Base64UrlOf("ticks_3c4d5e6f7a8b4c9d8e0f1a2b3c4d5e6f"),
        Base64UrlOf("-1_3c4d5e6f7a8b4c9d8e0f1a2b3c4d5e6f"),
        Base64UrlOf("3155378976000000000_3c4d5e6f7a8b4c9d8e0f1a2b3c4d5e6f"),
        Base64UrlOf("639033120000000000_not-a-guid")
    };

    private static string Base64UrlOf(string value)
    {
        return Base64Url.EncodeToString(Encoding.UTF8.GetBytes(value));
    }
}
