using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace Choco.IntegrationTests.Versioning;

public sealed class VersioningApiFactory : ChocoApiFactory
{
    public VersioningApiFactory(PostgresFixture postgres) : base(postgres) { }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ApiDocumentation:Enabled"] = "true",
                ["Clerk:PublishableKey"] = "pk_test_publishable-key"
            }));
    }
}
