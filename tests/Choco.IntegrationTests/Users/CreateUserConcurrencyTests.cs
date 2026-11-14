using Choco.Contract.Users;
using Choco.Domain.AuditLogs;
using Choco.Domain.Users;
using Choco.SharedKernel.Results;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Choco.IntegrationTests.Users;

public sealed class CreateUserConcurrencyTests : IClassFixture<ChocoApiFactory>, IAsyncLifetime
{
    private const int Callers = 8;

    private readonly ChocoApiFactory _factory;

    public CreateUserConcurrencyTests(ChocoApiFactory factory)
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
    public async Task SameIdentityCreatedAtTheSameTime_SucceedsForEveryCallerAndStoresOneUser()
    {
        Result[] results = await SendConcurrentlyAsync("identity-id");

        results.Should().OnlyContain(result => result.IsSuccess);
        List<User> users = await GetUsersAsync();
        users.Should().ContainSingle().Which.IdentityId.Should().Be("identity-id");
    }

    [Fact]
    public async Task SameIdentityCreatedAtTheSameTime_RecordsOneUserCreatedEntry()
    {
        await SendConcurrentlyAsync("identity-id");

        User user = (await GetUsersAsync()).Single();
        List<AuditLogEntry> entries = await _factory.ExecuteDbContextAsync(dbContext => dbContext.Set<AuditLogEntry>()
            .AsNoTracking()
            .ToListAsync(TestContext.Current.CancellationToken));
        AuditLogEntry entry = entries.Should().ContainSingle().Subject;
        entry.Action.Should().Be(AuditAction.UserCreated);
        entry.TargetId.Should().Be(user.Id);
    }

    [Fact]
    public async Task BootstrapAdministratorCreatedAtTheSameTime_HoldsTheAdministratorRoleOnce()
    {
        Result[] results = await SendConcurrentlyAsync(ChocoApiFactory.BootstrapAdminIdentityId);

        results.Should().OnlyContain(result => result.IsSuccess);
        User user = (await GetUsersAsync()).Single();
        user.Roles.Should().ContainSingle().Which.AssignedBy.Should().BeNull();
    }

    private async Task<Result[]> SendConcurrentlyAsync(string identityId)
    {
        using ManualResetEventSlim start = new(false);

        Task<Result>[] tasks = Enumerable.Range(0, Callers)
            .Select(_ => Task.Run(async () =>
            {
                using IServiceScope scope = _factory.Services.CreateScope();
                ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();
                start.Wait(TestContext.Current.CancellationToken);

                return await sender.Send(
                    new CreateUserCommand(identityId, "username", "Display Name", null),
                    TestContext.Current.CancellationToken);
            }, TestContext.Current.CancellationToken))
            .ToArray();

        start.Set();

        return await Task.WhenAll(tasks);
    }

    private Task<List<User>> GetUsersAsync()
    {
        return _factory.ExecuteDbContextAsync(dbContext => dbContext.Set<User>()
            .IgnoreQueryFilters()
            .Include(user => user.Roles)
            .AsNoTracking()
            .ToListAsync(TestContext.Current.CancellationToken));
    }
}
