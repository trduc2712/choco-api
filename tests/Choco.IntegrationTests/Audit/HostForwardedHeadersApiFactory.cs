using Microsoft.Extensions.Hosting;

namespace Choco.IntegrationTests.Audit;

public sealed class HostForwardedHeadersApiFactory : ChocoApiFactory
{
    private const string HostSetting = "ASPNETCORE_FORWARDEDHEADERS_ENABLED";

    public HostForwardedHeadersApiFactory(PostgresFixture postgres) : base(postgres) { }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        Environment.SetEnvironmentVariable(HostSetting, "true");

        try
        {
            return base.CreateHost(builder);
        }
        finally
        {
            Environment.SetEnvironmentVariable(HostSetting, null);
        }
    }
}
