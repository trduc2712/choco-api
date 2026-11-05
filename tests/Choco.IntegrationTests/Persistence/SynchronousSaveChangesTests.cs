using Choco.Domain.Users;

using Microsoft.EntityFrameworkCore;

namespace Choco.IntegrationTests.Persistence;

public sealed class SynchronousSaveChangesTests : IClassFixture<ChocoApiFactory>, IAsyncLifetime
{
    private readonly ChocoApiFactory _factory;

    public SynchronousSaveChangesTests(ChocoApiFactory factory)
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
    public async Task AddedEntity_GetsAuditTimestamps()
    {
        User user = User.Create("identity-id", "username", "Display Name", null);

        await _factory.ExecuteDbContextAsync(dbContext =>
        {
            dbContext.Add(user);
            return Task.FromResult(dbContext.SaveChanges());
        });

        User saved = await GetUserAsync(user.Id);
        saved.CreatedAt.Should().Be(ChocoApiFactory.StartTime);
        saved.ModifiedAt.Should().Be(ChocoApiFactory.StartTime);
    }

    [Fact]
    public async Task RemovedEntity_IsSoftDeleted()
    {
        User user = User.Create("identity-id", "username", "Display Name", null);
        await _factory.ExecuteDbContextAsync(dbContext =>
        {
            dbContext.Add(user);
            return dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        _factory.TimeProvider.Advance(TimeSpan.FromMinutes(1));

        await _factory.ExecuteDbContextAsync(dbContext =>
        {
            dbContext.Remove(dbContext.Set<User>().Single(u => u.Id == user.Id));
            return Task.FromResult(dbContext.SaveChanges());
        });

        User saved = await GetUserAsync(user.Id);
        saved.IsDeleted.Should().BeTrue();
        saved.DeletedAt.Should().Be(ChocoApiFactory.StartTime.AddMinutes(1));
        saved.ModifiedAt.Should().Be(ChocoApiFactory.StartTime.AddMinutes(1));
    }

    private Task<User> GetUserAsync(Guid id)
    {
        return _factory.ExecuteDbContextAsync(dbContext => dbContext.Set<User>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync(u => u.Id == id, TestContext.Current.CancellationToken));
    }
}
