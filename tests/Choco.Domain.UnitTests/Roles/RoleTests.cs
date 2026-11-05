using Choco.Domain.Roles;

namespace Choco.Domain.UnitTests.Roles;

public sealed class RoleTests
{
    private static readonly SystemRoleDefinition ValidDefinition =
        new("system-key", "Name", IsDefault: true, Permissions: [Permission.AssignRoles]);

    [Fact]
    public void CreateSystem_WithValidDefinition_ReturnsRoleWithThatData()
    {
        Role role = Role.CreateSystem(ValidDefinition);

        role.Id.Should().NotBeEmpty();
        role.SystemKey.Should().Be("system-key");
        role.Name.Should().Be("Name");
        role.IsDefault.Should().BeTrue();
        role.Permissions.Should().BeEquivalentTo([Permission.AssignRoles]);
    }

    [Fact]
    public void CreateSystem_WithRepeatedPermission_KeepsItOnce()
    {
        SystemRoleDefinition definition = ValidDefinition with
        {
            Permissions = [Permission.AssignRoles, Permission.AssignRoles]
        };

        Role role = Role.CreateSystem(definition);

        role.Permissions.Should().ContainSingle().Which.Should().Be(Permission.AssignRoles);
    }

    [Fact]
    public void CreateSystem_WithKeyAndNameAtMaxLength_ReturnsRole()
    {
        SystemRoleDefinition definition = ValidDefinition with
        {
            Key = new string('k', Role.SystemKeyMaxLength),
            Name = new string('n', Role.NameMaxLength)
        };

        Role role = Role.CreateSystem(definition);

        role.SystemKey.Should().Be(definition.Key);
        role.Name.Should().Be(definition.Name);
    }

    [Fact]
    public void CreateSystem_WithoutDefinition_ThrowsArgumentException()
    {
        Func<Role> act = () => Role.CreateSystem(null!);

        act.Should().Throw<ArgumentException>().WithParameterName("definition");
    }

    [Theory]
    [MemberData(nameof(InvalidDefinitions))]
    public void CreateSystem_WithInvalidDefinition_ThrowsArgumentException(SystemRoleDefinition definition)
    {
        Func<Role> act = () => Role.CreateSystem(definition);

        act.Should().Throw<ArgumentException>().WithParameterName("definition");
    }

    [Fact]
    public void SystemRoles_All_CreateValidRolesWithUniqueKeysAndOneDefault()
    {
        List<Role> roles = SystemRoles.All.Select(Role.CreateSystem).ToList();

        roles.Select(role => role.SystemKey).Should().OnlyHaveUniqueItems();
        roles.Should().ContainSingle(role => role.IsDefault);
    }

    [Fact]
    public void IsCoveredBy_WithEveryPermissionOfTheRole_ReturnsTrue()
    {
        Role role = Role.CreateSystem(ValidDefinition);

        bool isCovered = role.IsCoveredBy([Permission.AssignRoles, "other.permission"]);

        isCovered.Should().BeTrue();
    }

    [Fact]
    public void IsCoveredBy_WithoutAPermissionOfTheRole_ReturnsFalse()
    {
        Role role = Role.CreateSystem(ValidDefinition);

        bool isCovered = role.IsCoveredBy(["other.permission"]);

        isCovered.Should().BeFalse();
    }

    [Fact]
    public void IsCoveredBy_RoleWithoutPermissions_ReturnsTrue()
    {
        Role role = Role.CreateSystem(ValidDefinition with { Permissions = [] });

        bool isCovered = role.IsCoveredBy([]);

        isCovered.Should().BeTrue();
    }

    public static TheoryData<SystemRoleDefinition> InvalidDefinitions => new()
    {
        ValidDefinition with { Key = null! },
        ValidDefinition with { Key = "" },
        ValidDefinition with { Key = " " },
        ValidDefinition with { Key = new string('k', Role.SystemKeyMaxLength + 1) },
        ValidDefinition with { Name = null! },
        ValidDefinition with { Name = "" },
        ValidDefinition with { Name = " " },
        ValidDefinition with { Name = new string('n', Role.NameMaxLength + 1) },
        ValidDefinition with { Permissions = ["unknown.permission"] }
    };
}
