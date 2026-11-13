using System.Net;
using System.Reflection;
using System.Text.Json;

using Amazon.S3;
using Amazon.S3.Model;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Choco.IntegrationTests.Health;

public sealed class HealthTests : IClassFixture<ChocoApiFactory>, IAsyncLifetime
{
    private const string UnreachableDatabase = "Host=database.invalid;Port=1;Username=secret-user;Password=secret-password;Database=secret-db;Timeout=1";

    private readonly ChocoApiFactory _factory;

    public HealthTests(ChocoApiFactory factory)
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

    [Theory]
    [InlineData("/health")]
    [InlineData("/health/live")]
    public async Task Liveness_ReturnsHealthyWithoutTouchingTheDatabaseOrTheStorage(string path)
    {
        using HttpClient client = CreateClient(databaseConnectionString: UnreachableDatabase, storageWorks: false);

        using HttpResponseMessage response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().Be("Healthy");
    }

    [Fact]
    public async Task Readiness_WithWorkingDatabaseAndStorage_ReturnsHealthy()
    {
        using HttpClient client = CreateClient(databaseConnectionString: null, storageWorks: true);

        using HttpResponseMessage response = await client.GetAsync("/health/ready", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement body = await ReadBodyAsync(response);
        body.GetProperty("status").GetString().Should().Be("Healthy");
        body.GetProperty("checks").GetProperty("database").GetString().Should().Be("Healthy");
        body.GetProperty("checks").GetProperty("storage").GetString().Should().Be("Healthy");
    }

    [Fact]
    public async Task Readiness_WithFailingDatabase_ReturnsServiceUnavailableAndLeaksNothing()
    {
        using HttpClient client = CreateClient(databaseConnectionString: UnreachableDatabase, storageWorks: true);

        using HttpResponseMessage response = await client.GetAsync("/health/ready", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        string text = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        JsonElement body = JsonSerializer.Deserialize<JsonElement>(text);
        body.GetProperty("status").GetString().Should().Be("Unhealthy");
        body.GetProperty("checks").GetProperty("database").GetString().Should().Be("Unhealthy");
        body.GetProperty("checks").GetProperty("storage").GetString().Should().Be("Healthy");
        text.Should().NotContainAny("secret", "invalid", "Host", "Password", "Exception");
    }

    [Fact]
    public async Task Readiness_WithFailingStorage_ReturnsServiceUnavailableAndLeaksNothing()
    {
        using HttpClient client = CreateClient(databaseConnectionString: null, storageWorks: false);

        using HttpResponseMessage response = await client.GetAsync("/health/ready", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        string text = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        JsonElement body = JsonSerializer.Deserialize<JsonElement>(text);
        body.GetProperty("checks").GetProperty("database").GetString().Should().Be("Healthy");
        body.GetProperty("checks").GetProperty("storage").GetString().Should().Be("Unhealthy");
        text.Should().NotContainAny("localhost", "choco-test", "Simulated", "Exception");
    }

    private static async Task<JsonElement> ReadBodyAsync(HttpResponseMessage response)
    {
        return JsonSerializer.Deserialize<JsonElement>(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    private HttpClient CreateClient(string? databaseConnectionString, bool storageWorks)
    {
        return _factory.WithWebHostBuilder(builder =>
        {
            if (databaseConnectionString is not null)
            {
                builder.ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(
                    new Dictionary<string, string?> { ["Database:ConnectionString"] = databaseConnectionString }));
            }

            builder.ConfigureTestServices(services => services.Replace(ServiceDescriptor.Singleton(
                _ => storageWorks
                    ? DispatchProxy.Create<IAmazonS3, WorkingStorageProxy>()
                    : DispatchProxy.Create<IAmazonS3, FailingStorageProxy>())));
        }).CreateClient();
    }

    public class WorkingStorageProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            return targetMethod?.Name switch
            {
                nameof(IAmazonS3.ListObjectsV2Async) => Task.FromResult(new ListObjectsV2Response()),
                nameof(IDisposable.Dispose) => null,
                _ => throw new NotSupportedException(targetMethod?.Name)
            };
        }
    }

    public class FailingStorageProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == nameof(IDisposable.Dispose))
            {
                return null;
            }

            throw new HttpRequestException("Simulated failure for http://localhost:9000 choco-test-uploads.");
        }
    }
}
