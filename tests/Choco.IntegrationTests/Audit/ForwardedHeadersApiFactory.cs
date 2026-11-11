using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace Choco.IntegrationTests.Audit;

public sealed class ForwardedHeadersApiFactory : ChocoApiFactory
{
    public ForwardedHeadersApiFactory(PostgresFixture postgres) : base(postgres) { }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ForwardedHeaders:Enabled"] = "true",
                ["ForwardedHeaders:TrustAnyProxy"] = "true",
                ["ForwardedHeaders:ForwardLimit"] = "1"
            }));
    }
}
