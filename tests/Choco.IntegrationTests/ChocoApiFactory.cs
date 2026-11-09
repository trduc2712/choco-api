using Choco.Application.Abstractions.Authentication;
using Choco.Infrastructure.Persistence;
using Choco.Infrastructure.Persistence.Seeding;
using Choco.IntegrationTests.Authentication;
using Choco.IntegrationTests.Webhooks;
using Choco.Presentation.Authorization;

using MartinCostello.Logging.XUnit;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

using Npgsql;

using Respawn;

namespace Choco.IntegrationTests;

public class ChocoApiFactory : WebApplicationFactory<Program>, IAsyncLifetime, ITestOutputHelperAccessor
{
    public const string IdentityProviderSecretKey = "sk_test_secret-key";
    public const string BootstrapAdminIdentityId = "bootstrap-identity-id";

    public static readonly DateTimeOffset StartTime = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    public static readonly Uri IdentityProviderUrl = new("https://identity-provider.example.com/v1/");

    private readonly PostgresFixture _postgres;

    private string _connectionString = null!;
    private Respawner _respawner = null!;

    public ChocoApiFactory(PostgresFixture postgres)
    {
        _postgres = postgres;
        ClientOptions.AllowAutoRedirect = false;
    }

    public FakeTimeProvider TimeProvider { get; } = new(StartTime);

    public ITestOutputHelper? OutputHelper { get; set; }

    internal StubHttpMessageHandler IdentityProvider { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        UseTestSettings(builder, _connectionString);

        builder.ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Authorization:BootstrapAdminIdentityIds:0"] = BootstrapAdminIdentityId
            }));

        builder.ConfigureLogging(logging => logging.ClearProviders().AddXUnit(this));

        builder.ConfigureTestServices(services =>
        {
            services.Remove(services.Single(descriptor =>
                descriptor.ImplementationType == typeof(AdministratorBootstrapHostedService)));
            services.Replace(ServiceDescriptor.Singleton<TimeProvider>(TimeProvider));
            services.AddHttpClient(nameof(IIdentityProvider))
                .ConfigurePrimaryHttpMessageHandler(() => IdentityProvider);
            services.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.Configuration = new OpenIdConnectConfiguration { Issuer = TestJwt.Issuer };
                options.Configuration.SigningKeys.Add(TestJwt.SigningKey);
            });
        });
    }

    public async ValueTask InitializeAsync()
    {
        _connectionString = await _postgres.CreateDatabaseAsync();

        await using NpgsqlConnection connection = await OpenConnectionAsync();

        _respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = ["public"],
            TablesToIgnore = ["__EFMigrationsHistory"]
        });
    }

    public async Task ResetAsync()
    {
        OutputHelper = TestContext.Current.TestOutputHelper;
        TimeProvider.AdjustTime(StartTime);
        IdentityProvider.Reset();

        await using NpgsqlConnection connection = await OpenConnectionAsync();

        await _respawner.ResetAsync(connection);

        await ExecuteDbContextAsync(async dbContext =>
        {
            await SystemRoleSeeder.SeedAsync(dbContext, true, TestContext.Current.CancellationToken);
            return 0;
        });
    }

    internal async Task<T> ExecuteDbContextAsync<T>(Func<ChocoDbContext, Task<T>> action)
    {
        using IServiceScope scope = Services.CreateScope();
        ChocoDbContext dbContext = scope.ServiceProvider.GetRequiredService<ChocoDbContext>();

        return await action(dbContext);
    }

    internal static void UseTestSettings(IWebHostBuilder builder, string connectionString)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Database:ConnectionString"] = connectionString,
                ["Clerk:Authority"] = TestJwt.Issuer,
                ["Clerk:AuthorizedParties:0"] = TestJwt.AuthorizedParty,
                ["Clerk:WebhookSigningSecret"] = ClerkWebhookSigner.SigningSecret,
                ["Clerk:SecretKey"] = IdentityProviderSecretKey,
                ["Clerk:BackendApiUrl"] = IdentityProviderUrl.ToString(),
                ["Storage:ServiceUrl"] = "http://localhost:9000",
                ["Storage:Region"] = "us-east-1",
                ["Storage:UploadsBucketName"] = "choco-test-uploads",
                ["Storage:MediaBucketName"] = "choco-test-media",
                ["Storage:AccessKeyId"] = "test-access-key",
                ["Storage:SecretAccessKey"] = "test-secret-key"
            }));
    }

    private async Task<NpgsqlConnection> OpenConnectionAsync()
    {
        NpgsqlConnection connection = new(_connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        return connection;
    }
}
