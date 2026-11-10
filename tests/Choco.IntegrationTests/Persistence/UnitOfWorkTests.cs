using Choco.Domain.Primitives.Persistence;

using Microsoft.Extensions.DependencyInjection;

namespace Choco.IntegrationTests.Persistence;

public sealed class UnitOfWorkTests : IClassFixture<ChocoApiFactory>, IAsyncLifetime
{
    private readonly ChocoApiFactory _factory;

    public UnitOfWorkTests(ChocoApiFactory factory)
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
    public async Task AfterCommit_InTransaction_RunsOnlyAfterTheCommit()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        IUnitOfWork unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        List<string> steps = [];

        await unitOfWork.InTransactionAsync(async () =>
        {
            await unitOfWork.AfterCommitAsync(_ =>
            {
                steps.Add("after-commit");
                return Task.CompletedTask;
            }, TestContext.Current.CancellationToken);
            steps.Add("action");

            return true;
        }, TestContext.Current.CancellationToken);

        steps.Should().Equal("action", "after-commit");
    }

    [Fact]
    public async Task AfterCommit_WhenTheTransactionIsRolledBack_NeverRuns()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        IUnitOfWork unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        bool ran = false;

        Func<Task> failing = () => unitOfWork.InTransactionAsync<bool>(async () =>
        {
            await unitOfWork.AfterCommitAsync(_ =>
            {
                ran = true;
                return Task.CompletedTask;
            }, TestContext.Current.CancellationToken);

            throw new InvalidOperationException("action-failed");
        }, TestContext.Current.CancellationToken);

        await failing.Should().ThrowAsync<InvalidOperationException>();
        ran.Should().BeFalse();
    }

    [Fact]
    public async Task AfterCommit_WithoutTransaction_RunsImmediately()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        IUnitOfWork unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        bool ran = false;

        await unitOfWork.AfterCommitAsync(_ =>
        {
            ran = true;
            return Task.CompletedTask;
        }, TestContext.Current.CancellationToken);

        ran.Should().BeTrue();
    }
}
