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
    public void Create_WithValidData_ReturnsRoleWithoutKeyThatIsNotDefault()
    {
        Role role = Role.Create("Name", [Permission.AssignRoles]);

        role.Id.Should().NotBeEmpty();
        role.Name.Should().Be("Name");
        role.SystemKey.Should().BeNull();
        role.IsDefault.Should().BeFalse();
        role.Permissions.Should().BeEquivalentTo([Permission.AssignRoles]);
    }

    [Fact]
    public void Create_WithSurroundingWhitespace_TrimsTheName()
    {
        Role role = Role.Create("  Name  ", []);

        role.Name.Should().Be("Name");
    }

    [Fact]
    public void Create_WithoutPermissions_ReturnsRoleWithNone()
    {
        Role role = Role.Create("Name", []);

        role.Permissions.Should().BeEmpty();
    }

    [Fact]
    public void Create_WithRepeatedPermission_KeepsItOnce()
    {
        Role role = Role.Create("Name", [Permission.AssignRoles, Permission.AssignRoles]);

        role.Permissions.Should().ContainSingle().Which.Should().Be(Permission.AssignRoles);
    }

    [Fact]
    public void Create_WithNameAtMaxLength_ReturnsRole()
    {
        string name = new('n', Role.NameMaxLength);

        Role role = Role.Create(name, []);

        role.Name.Should().Be(name);
    }

    [Fact]
    public void Create_WithNameAtMaxLengthAfterTrimming_ReturnsRole()
    {
        string name = new('n', Role.NameMaxLength);

        Role role = Role.Create($" {name} ", []);

        role.Name.Should().Be(name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Create_WithBlankName_ThrowsArgumentException(string? name)
    {
        Func<Role> act = () => Role.Create(name!, []);

        act.Should().Throw<ArgumentException>().WithParameterName("name");
    }

    [Fact]
    public void Create_WithNameOverMaxLength_ThrowsArgumentOutOfRangeException()
    {
        Func<Role> act = () => Role.Create(new string('n', Role.NameMaxLength + 1), []);

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("name");
    }

    [Fact]
    public void Create_WithoutPermissionList_ThrowsArgumentNullException()
    {
        Func<Role> act = () => Role.Create("Name", null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("permissions");
    }

    [Fact]
    public void Create_WithUnknownPermission_ThrowsArgumentException()
    {
        Func<Role> act = () => Role.Create("Name", ["unknown.permission"]);

        act.Should().Throw<ArgumentException>().WithParameterName("permissions");
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
