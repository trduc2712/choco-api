using Choco.Domain.Roles;
using Choco.Domain.Users;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Choco.IntegrationTests.Persistence;

public sealed class RoleRepositoryTests : IClassFixture<ChocoApiFactory>, IAsyncLifetime
{
    private readonly ChocoApiFactory _factory;

    public RoleRepositoryTests(ChocoApiFactory factory)
    {
        _factory = factory;
    }

    public async ValueTask InitializeAsync()
    {
        await _factory.ResetAsync();
        await _factory.ExecuteDbContextAsync(async dbContext =>
        {
            dbContext.Set<Role>().RemoveRange(await dbContext.Set<Role>().ToListAsync(TestContext.Current.CancellationToken));
            return await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task UserWithoutAssignedRoles_GetsDefaultRolePermissions()
    {
        User user = CreateUser("identity-id", "username");
        await AddAsync(CreateRole("default-role", isDefault: true, [Permission.AssignRoles]), user);

        IReadOnlyCollection<string> permissions = await GetPermissionsAsync(user.Id);

        permissions.Should().BeEquivalentTo([Permission.AssignRoles]);
    }

    [Fact]
    public async Task AssignedRole_AddsItsPermissions()
    {
        Role assignedRole = CreateRole("assigned-role", isDefault: false, [Permission.AssignRoles]);
        User user = CreateUser("identity-id", "username");
        user.AssignRole(assignedRole.Id, null, ChocoApiFactory.StartTime);
        await AddAsync(CreateRole("default-role", isDefault: true, []), assignedRole, user);

        IReadOnlyCollection<string> permissions = await GetPermissionsAsync(user.Id);

        permissions.Should().BeEquivalentTo([Permission.AssignRoles]);
    }

    [Fact]
    public async Task RoleNotAssignedToUser_GrantsNothing()
    {
        User user = CreateUser("identity-id", "username");
        await AddAsync(
            CreateRole("default-role", isDefault: true, []),
            CreateRole("unassigned-role", isDefault: false, [Permission.AssignRoles]),
            user);

        IReadOnlyCollection<string> permissions = await GetPermissionsAsync(user.Id);

        permissions.Should().BeEmpty();
    }

    [Fact]
    public async Task RoleAssignedToAnotherUser_GrantsNothing()
    {
        Role assignedRole = CreateRole("assigned-role", isDefault: false, [Permission.AssignRoles]);
        User user = CreateUser("identity-id-1", "username-1");
        User otherUser = CreateUser("identity-id-2", "username-2");
        otherUser.AssignRole(assignedRole.Id, null, ChocoApiFactory.StartTime);
        await AddAsync(CreateRole("default-role", isDefault: true, []), assignedRole, user, otherUser);

        IReadOnlyCollection<string> permissions = await GetPermissionsAsync(user.Id);

        permissions.Should().BeEmpty();
    }

    [Fact]
    public async Task PermissionFromSeveralRoles_IsReturnedOnce()
    {
        Role assignedRole = CreateRole("assigned-role", isDefault: false, [Permission.AssignRoles]);
        User user = CreateUser("identity-id", "username");
        user.AssignRole(assignedRole.Id, null, ChocoApiFactory.StartTime);
        await AddAsync(CreateRole("default-role", isDefault: true, [Permission.AssignRoles]), assignedRole, user);

        IReadOnlyCollection<string> permissions = await GetPermissionsAsync(user.Id);

        permissions.Should().ContainSingle().Which.Should().Be(Permission.AssignRoles);
    }

    private static Role CreateRole(string key, bool isDefault, IReadOnlyCollection<string> permissions)
    {
        return Role.CreateSystem(new SystemRoleDefinition(key, key, isDefault, permissions));
    }

    private static User CreateUser(string identityId, string username)
    {
        return User.Create(identityId, username, "Display Name", null);
    }

    private Task AddAsync(params object[] entities)
    {
        return _factory.ExecuteDbContextAsync(dbContext =>
        {
            dbContext.AddRange(entities);
            return dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
    }

    private async Task<IReadOnlyCollection<string>> GetPermissionsAsync(Guid userId)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        IRoleRepository roleRepository = scope.ServiceProvider.GetRequiredService<IRoleRepository>();

        return await roleRepository.GetPlatformPermissionsForReadAsync(userId, TestContext.Current.CancellationToken);
    }
}
