using Choco.Contract.Users;

using MediatR;

namespace Choco.Presentation.Authorization;

internal sealed class AdministratorBootstrapHostedService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;

    public AdministratorBootstrapHostedService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using IServiceScope scope = _scopeFactory.CreateScope();
        ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();

        await sender.Send(new BootstrapAdministratorsCommand(), cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
