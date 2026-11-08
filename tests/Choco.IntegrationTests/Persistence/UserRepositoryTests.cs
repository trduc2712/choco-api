using Choco.Domain.Users;

using Microsoft.Extensions.DependencyInjection;

namespace Choco.IntegrationTests.Persistence;

public sealed class UserRepositoryTests : IClassFixture<ChocoApiFactory>, IAsyncLifetime
{
    private readonly ChocoApiFactory _factory;

    public UserRepositoryTests(ChocoApiFactory factory)
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
    public async Task ListByIds_ReturnsOnlyTheRequestedUsers()
    {
        User first = await AddUserAsync("identity-id", "username");
        User second = await AddUserAsync("identity-id-2", "username-2");
        await AddUserAsync("identity-id-3", "username-3");

        IReadOnlyList<User> users = await ListByIdsAsync([first.Id, second.Id]);

        users.Select(user => user.Id).Should().BeEquivalentTo([first.Id, second.Id]);
    }

    [Fact]
    public async Task ListByIds_SkipsDeletedAndUnknownUsers()
    {
        User kept = await AddUserAsync("identity-id", "username");
        User deleted = await AddUserAsync("identity-id-2", "username-2");
        await _factory.ExecuteDbContextAsync(dbContext =>
        {
            dbContext.Remove(dbContext.Set<User>().Single(user => user.Id == deleted.Id));
            return dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        IReadOnlyList<User> users = await ListByIdsAsync([kept.Id, deleted.Id, new Guid("00000000-0000-0000-0000-000000000001")]);

        users.Select(user => user.Id).Should().Equal(kept.Id);
    }

    [Fact]
    public async Task ListByIds_WithoutIds_ReturnsNothing()
    {
        await AddUserAsync("identity-id", "username");

        IReadOnlyList<User> users = await ListByIdsAsync([]);

        users.Should().BeEmpty();
    }

    private async Task<User> AddUserAsync(string identityId, string username)
    {
        User user = User.Create(identityId, username, "Display Name", null);
        await _factory.ExecuteDbContextAsync(dbContext =>
        {
            dbContext.Add(user);
            return dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        return user;
    }

    private async Task<IReadOnlyList<User>> ListByIdsAsync(IReadOnlyCollection<Guid> ids)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        IUserRepository repository = scope.ServiceProvider.GetRequiredService<IUserRepository>();

        return await repository.ListByIdsForReadAsync(ids, TestContext.Current.CancellationToken);
    }
}
