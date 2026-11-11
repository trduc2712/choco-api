using Choco.Domain.Users;

namespace Choco.Domain.UnitTests.Users;

public sealed class UserTests
{
    private static readonly Guid RoleId = new("6b1e0f3a-2c4d-4e8f-9a7b-1d2c3e4f5a60");
    private static readonly Guid OtherRoleId = new("7c2f1a4b-3d5e-4f9a-8b6c-2e3d4f5a6b70");
    private static readonly Guid AssignedBy = new("8d3a2b5c-4e6f-4a1b-9c7d-3f4a5b6c7d80");
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

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
        user.Roles.Should().BeEmpty();
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

    [Theory]
    [InlineData("Display Name 2", "Display Name 2")]
    [InlineData(null, "username-2")]
    public void ChangeProfileName_WithNewUsername_UpdatesNameAndKeepsAvatar(string? displayName, string expectedName)
    {
        const string avatarUrl = "https://example.com/avatar.png";
        User user = User.Create("identity-id", "username", "Display Name", avatarUrl);

        user.ChangeProfileName("username-2", displayName);

        user.Username.Should().Be("username-2");
        user.DisplayName.Should().Be(expectedName);
        user.AvatarUrl.Should().Be(avatarUrl);
    }

    [Theory]
    [InlineData("https://example.com/avatar-2.png")]
    [InlineData(null)]
    public void ChangeAvatar_WithNewValue_UpdatesAvatarAndKeepsName(string? avatarUrl)
    {
        User user = User.Create("identity-id", "username", "Display Name", "https://example.com/avatar.png");

        user.ChangeAvatar(avatarUrl);

        user.AvatarUrl.Should().Be(avatarUrl);
        user.Username.Should().Be("username");
        user.DisplayName.Should().Be("Display Name");
    }

    [Theory]
    [InlineData("Bio", "Bio")]
    [InlineData("  Bio  ", "Bio")]
    [InlineData("", null)]
    [InlineData(" ", null)]
    [InlineData(null, null)]
    public void ChangeBio_WithValue_StoresTrimmedBioOrNull(string? bio, string? expectedBio)
    {
        User user = User.Create("identity-id", "username", "Display Name", null);

        user.ChangeBio(bio);

        user.Bio.Should().Be(expectedBio);
    }

    [Fact]
    public void ChangeProfileName_WithBioSet_KeepsBio()
    {
        User user = User.Create("identity-id", "username", "Display Name", null);
        user.ChangeBio("Bio");

        user.ChangeProfileName("username-2", "Display Name 2");

        user.Bio.Should().Be("Bio");
    }

    [Fact]
    public void AssignRole_NewRole_AddsAssignment()
    {
        User user = User.Create("identity-id", "username", "Display Name", null);

        user.AssignRole(RoleId, AssignedBy, Now);

        UserRole assignment = user.Roles.Should().ContainSingle().Subject;
        assignment.Id.Should().NotBeEmpty();
        assignment.RoleId.Should().Be(RoleId);
        assignment.AssignedBy.Should().Be(AssignedBy);
        assignment.AssignedAt.Should().Be(Now);
    }

    [Fact]
    public void AssignRole_WithoutAssigner_RecordsNoAssigner()
    {
        User user = User.Create("identity-id", "username", "Display Name", null);

        user.AssignRole(RoleId, null, Now);

        user.Roles.Should().ContainSingle().Which.AssignedBy.Should().BeNull();
    }

    [Fact]
    public void AssignRole_SecondRole_KeepsBothRoles()
    {
        User user = User.Create("identity-id", "username", "Display Name", null);
        user.AssignRole(RoleId, AssignedBy, Now);

        user.AssignRole(OtherRoleId, AssignedBy, Now);

        user.Roles.Select(role => role.RoleId).Should().BeEquivalentTo([RoleId, OtherRoleId]);
    }

    [Fact]
    public void AssignRole_RoleAlreadyHeld_KeepsFirstAssignment()
    {
        User user = User.Create("identity-id", "username", "Display Name", null);
        user.AssignRole(RoleId, AssignedBy, Now);

        user.AssignRole(RoleId, null, Now.AddDays(1));

        UserRole assignment = user.Roles.Should().ContainSingle().Subject;
        assignment.AssignedBy.Should().Be(AssignedBy);
        assignment.AssignedAt.Should().Be(Now);
    }

    [Fact]
    public void AssignRole_WithEmptyRoleId_ThrowsArgumentExceptionAndKeepsRoles()
    {
        User user = User.Create("identity-id", "username", "Display Name", null);
        user.AssignRole(RoleId, AssignedBy, Now);

        Action act = () => user.AssignRole(Guid.Empty, AssignedBy, Now);

        act.Should().Throw<ArgumentException>().WithParameterName("roleId");
        user.Roles.Should().ContainSingle().Which.RoleId.Should().Be(RoleId);
    }

    [Fact]
    public void RevokeRole_HeldRole_RemovesOnlyThatRole()
    {
        User user = User.Create("identity-id", "username", "Display Name", null);
        user.AssignRole(RoleId, AssignedBy, Now);
        user.AssignRole(OtherRoleId, AssignedBy, Now);

        user.RevokeRole(RoleId);

        user.Roles.Should().ContainSingle().Which.RoleId.Should().Be(OtherRoleId);
    }

    [Fact]
    public void HoldsRole_AssignedRole_ReturnsTrue()
    {
        User user = User.Create("identity-id", "username", "Display Name", null);
        user.AssignRole(RoleId, AssignedBy, Now);

        bool holdsRole = user.HoldsRole(RoleId);

        holdsRole.Should().BeTrue();
    }

    [Fact]
    public void HoldsRole_RoleNotAssigned_ReturnsFalse()
    {
        User user = User.Create("identity-id", "username", "Display Name", null);
        user.AssignRole(RoleId, AssignedBy, Now);

        bool holdsRole = user.HoldsRole(OtherRoleId);

        holdsRole.Should().BeFalse();
    }

    [Fact]
    public void RevokeRole_RoleNotHeld_KeepsRoles()
    {
        User user = User.Create("identity-id", "username", "Display Name", null);
        user.AssignRole(RoleId, AssignedBy, Now);

        user.RevokeRole(OtherRoleId);

        user.Roles.Should().ContainSingle().Which.RoleId.Should().Be(RoleId);
    }
}
