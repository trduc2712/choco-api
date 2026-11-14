using Choco.Domain.Users;

namespace Choco.Domain.UnitTests.Users;

public sealed class UserIdentitySyncTests
{
    private static readonly DateTimeOffset Synced = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_WithIdentityUpdatedAt_StoresIt()
    {
        User user = User.Create("identity-id", "username", "Display Name", null, Synced);

        user.IdentityUpdatedAt.Should().Be(Synced);
    }

    [Fact]
    public void Create_WithoutIdentityUpdatedAt_LeavesItNull()
    {
        User user = User.Create("identity-id", "username", "Display Name", null);

        user.IdentityUpdatedAt.Should().BeNull();
    }

    [Fact]
    public void SyncFromIdentity_ForAUserNeverSynced_AppliesTheEvent()
    {
        User user = User.Create("identity-id", "username", "Display Name", null);

        bool applied = user.SyncFromIdentity("new-username", "New Name", "https://example.com/new.png", Synced);

        applied.Should().BeTrue();
        user.Username.Should().Be("new-username");
        user.DisplayName.Should().Be("New Name");
        user.AvatarUrl.Should().Be("https://example.com/new.png");
        user.IdentityUpdatedAt.Should().Be(Synced);
    }

    [Fact]
    public void SyncFromIdentity_WithALaterTime_AppliesTheEvent()
    {
        User user = User.Create("identity-id", "username", "Display Name", null, Synced);

        bool applied = user.SyncFromIdentity("new-username", "New Name", null, Synced.AddMilliseconds(1));

        applied.Should().BeTrue();
        user.Username.Should().Be("new-username");
        user.AvatarUrl.Should().BeNull();
        user.IdentityUpdatedAt.Should().Be(Synced.AddMilliseconds(1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void SyncFromIdentity_WithTheSameOrAnEarlierTime_ChangesNothing(int milliseconds)
    {
        User user = User.Create("identity-id", "username", "Display Name", "https://example.com/avatar.png", Synced);

        bool applied = user.SyncFromIdentity("new-username", "New Name", null, Synced.AddMilliseconds(milliseconds));

        applied.Should().BeFalse();
        user.Username.Should().Be("username");
        user.DisplayName.Should().Be("Display Name");
        user.AvatarUrl.Should().Be("https://example.com/avatar.png");
        user.IdentityUpdatedAt.Should().Be(Synced);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void SyncFromIdentity_WithoutADisplayName_UsesTheUsernameAsDisplayName(string? displayName)
    {
        User user = User.Create("identity-id", "username", "Display Name", null);

        user.SyncFromIdentity("new-username", displayName, null, Synced);

        user.DisplayName.Should().Be("new-username");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void SyncFromIdentity_WithoutAUsername_ThrowsArgumentException(string? username)
    {
        User user = User.Create("identity-id", "username", "Display Name", null);

        Action act = () => user.SyncFromIdentity(username!, "Display Name", null, Synced);

        act.Should().Throw<ArgumentException>().WithParameterName("username");
    }

    [Theory]
    [InlineData(-1, true)]
    [InlineData(0, true)]
    [InlineData(1, false)]
    public void IsStaleIdentityEvent_ComparesWithTheStoredTime(int milliseconds, bool expected)
    {
        User user = User.Create("identity-id", "username", "Display Name", null, Synced);

        user.IsStaleIdentityEvent(Synced.AddMilliseconds(milliseconds)).Should().Be(expected);
    }

    [Fact]
    public void IsStaleIdentityEvent_ForAUserNeverSynced_IsFalse()
    {
        User user = User.Create("identity-id", "username", "Display Name", null);

        user.IsStaleIdentityEvent(Synced).Should().BeFalse();
    }
}
