using System.Globalization;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace Choco.IntegrationTests.RateLimiting;

public sealed class RateLimitingApiFactory : ChocoApiFactory
{
    public const int TokenLimit = 2;

    public RateLimitingApiFactory(PostgresFixture postgres) : base(postgres) { }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["RateLimiting:TokenLimit"] = TokenLimit.ToString(CultureInfo.InvariantCulture),
                ["RateLimiting:TokensPerPeriod"] = "1",
                ["RateLimiting:ReplenishmentPeriod"] = "01:00:00"
            }));
    }
}
