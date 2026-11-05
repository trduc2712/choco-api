using Choco.Contract.Users;
using Choco.Domain.Roles;
using Choco.Domain.Users;
using Choco.SharedKernel.Results;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Choco.IntegrationTests.Users;

public sealed class BootstrapAdministratorsTests : IClassFixture<ChocoApiFactory>, IAsyncLifetime
{
    private readonly ChocoApiFactory _factory;

    public BootstrapAdministratorsTests(ChocoApiFactory factory)
    {
        _factory = factory;
    }

    public async ValueTask InitializeAsync()
    {
        await _factory.ResetAsync();
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task ListedUser_GetsAdministratorRoleFromTheSystem()
    {
        Guid adminRoleId = await GetRoleIdAsync(SystemRoles.Admin);
        User user = await AddUserAsync(ChocoApiFactory.BootstrapAdminIdentityId, "username");

        Result result = await SendAsync();

        result.IsSuccess.Should().BeTrue();
        UserRole assignment = (await GetUserAsync(user.Id)).Roles.Should().ContainSingle().Subject;
        assignment.RoleId.Should().Be(adminRoleId);
        assignment.AssignedBy.Should().BeNull();
        assignment.AssignedAt.Should().Be(ChocoApiFactory.StartTime);
    }

    [Fact]
    public async Task ListedUserAlreadyAdministrator_KeepsSingleAssignment()
    {
        Guid adminRoleId = await GetRoleIdAsync(SystemRoles.Admin);
        User user = await AddUserAsync(ChocoApiFactory.BootstrapAdminIdentityId, "username", adminRoleId);
        _factory.TimeProvider.Advance(TimeSpan.FromMinutes(1));

        Result result = await SendAsync();

        result.IsSuccess.Should().BeTrue();
        UserRole assignment = (await GetUserAsync(user.Id)).Roles.Should().ContainSingle().Subject;
        assignment.AssignedAt.Should().Be(ChocoApiFactory.StartTime);
    }

    [Fact]
    public async Task UnlistedUser_GetsNoRole()
    {
        User user = await AddUserAsync("identity-id", "username");

        Result result = await SendAsync();

        result.IsSuccess.Should().BeTrue();
        (await GetUserAsync(user.Id)).Roles.Should().BeEmpty();
    }

    [Fact]
    public async Task DeletedListedUser_GetsNoRole()
    {
        User user = await AddUserAsync(ChocoApiFactory.BootstrapAdminIdentityId, "username");
        await _factory.ExecuteDbContextAsync(dbContext =>
        {
            dbContext.Remove(dbContext.Set<User>().Single(u => u.Id == user.Id));
            return dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        Result result = await SendAsync();

        result.IsSuccess.Should().BeTrue();
        (await GetUserAsync(user.Id)).Roles.Should().BeEmpty();
    }

    private async Task<Result> SendAsync()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();

        return await sender.Send(new BootstrapAdministratorsCommand(), TestContext.Current.CancellationToken);
    }

    private Task<Guid> GetRoleIdAsync(string systemKey)
    {
        return _factory.ExecuteDbContextAsync(dbContext => dbContext.Set<Role>()
            .Where(role => role.SystemKey == systemKey)
            .Select(role => role.Id)
            .SingleAsync(TestContext.Current.CancellationToken));
    }

    private async Task<User> AddUserAsync(string identityId, string username, Guid? roleId = null)
    {
        User user = User.Create(identityId, username, "Display Name", null);

        if (roleId is not null)
        {
            user.AssignRole(roleId.Value, null, ChocoApiFactory.StartTime);
        }

        await _factory.ExecuteDbContextAsync(dbContext =>
        {
            dbContext.Add(user);
            return dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        return user;
    }

    private Task<User> GetUserAsync(Guid id)
    {
        return _factory.ExecuteDbContextAsync(dbContext => dbContext.Set<User>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(user => user.Roles)
            .SingleAsync(user => user.Id == id, TestContext.Current.CancellationToken));
    }
}
