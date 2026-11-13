using Choco.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Choco.Infrastructure.Health;

internal sealed class DatabaseHealthCheck(ChocoDbContext dbContext) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        bool canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);

        return canConnect ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy();
    }
}
