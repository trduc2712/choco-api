using Choco.Domain.Users;

namespace Choco.Domain.UnitTests.Users;

public sealed class UserTests
{
    [Fact]
    public void Create_WithValidData_ReturnsUserWithThatData()
    {
        string identityId = "identity-id";
        string username = "username";
        string displayName = "Display Name";
        string avatarUrl = "https://example.com/avatar.png";

        User user = User.Create(identityId, username, displayName, avatarUrl);

        user.Id.Should().NotBeEmpty();
        user.IdentityId.Should().Be(identityId);
        user.Username.Should().Be(username);
        user.DisplayName.Should().Be(displayName);
        user.AvatarUrl.Should().Be(avatarUrl);
        user.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public void Create_WithoutAvatarUrl_ReturnsUserWithNullAvatarUrl()
    {
        User user = User.Create("identity-id", "username", "Display Name", null);

        user.AvatarUrl.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Create_WithoutDisplayName_UsesUsernameAsDisplayName(string? displayName)
    {
        string identityId = "identity-id";
        string username = "username";
        string avatarUrl = "https://example.com/avatar.png";

        User user = User.Create(identityId, username, displayName, avatarUrl);

        user.Id.Should().NotBeEmpty();
        user.IdentityId.Should().Be(identityId);
        user.Username.Should().Be(username);
        user.DisplayName.Should().Be(username);
        user.AvatarUrl.Should().Be(avatarUrl);
        user.IsDeleted.Should().BeFalse();
    }

    [Theory]
    [InlineData(null, "username", "identityId")]
    [InlineData("", "username", "identityId")]
    [InlineData(" ", "username", "identityId")]
    [InlineData("identity-id", null, "username")]
    [InlineData("identity-id", "", "username")]
    [InlineData("identity-id", " ", "username")]
    public void Create_WithBlankRequiredValue_ThrowsArgumentException(
        string? identityId,
        string? username,
        string expectedParamName)
    {
        Func<User> act = () => User.Create(identityId!, username!, "Display Name", null);

        act.Should().Throw<ArgumentException>().WithParameterName(expectedParamName);
    }
}
