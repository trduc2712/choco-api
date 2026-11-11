using System.Text.RegularExpressions;

using Choco.Infrastructure.Persistence;
using Choco.IntegrationTests;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Npgsql;

using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(PostgresFixture))]

namespace Choco.IntegrationTests;

public sealed partial class PostgresFixture : IAsyncLifetime
{
    private const string TemplateDatabase = "choco_template";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder(FindComposePostgresImage())
        .WithCommand("-c", "max_connections=300")
        .Build();
    private readonly SemaphoreSlim _createDatabaseLock = new(1, 1);

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync(TestContext.Current.CancellationToken);

        await ExecuteAsync($"CREATE DATABASE {TemplateDatabase}");

        await using (WebApplicationFactory<Program> host = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            ChocoApiFactory.UseTestSettings(builder, GetConnectionString(TemplateDatabase))))
        {
            using IServiceScope scope = host.Services.CreateScope();
            ChocoDbContext dbContext = scope.ServiceProvider.GetRequiredService<ChocoDbContext>();

            await dbContext.Database.MigrateAsync(TestContext.Current.CancellationToken);
        }

        NpgsqlConnection.ClearAllPools();
    }

    public async Task<string> CreateDatabaseAsync()
    {
        string database = $"choco_{Guid.NewGuid():N}";

        await _createDatabaseLock.WaitAsync(TestContext.Current.CancellationToken);

        try
        {
            await ExecuteAsync($"CREATE DATABASE {database} TEMPLATE {TemplateDatabase}");
        }
        finally
        {
            _createDatabaseLock.Release();
        }

        return GetConnectionString(database);
    }

    public async ValueTask DisposeAsync()
    {
        await _container.DisposeAsync();
        _createDatabaseLock.Dispose();
    }

    private string GetConnectionString(string database)
    {
        return new NpgsqlConnectionStringBuilder(_container.GetConnectionString()) { Database = database }.ConnectionString;
    }

    private async Task ExecuteAsync(string sql)
    {
        string connectionString = new NpgsqlConnectionStringBuilder(_container.GetConnectionString()) { Pooling = false }.ConnectionString;

        await using NpgsqlConnection connection = new(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        await using NpgsqlCommand command = new(sql, connection);
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private static string FindComposePostgresImage()
    {
        string composeFile = Path.Combine(FindRepositoryRoot(), "compose.yaml");
        Match match = ComposePostgresImageRegex().Match(File.ReadAllText(composeFile));

        if (!match.Success)
        {
            throw new InvalidOperationException($"No 'image: postgres:<tag>' line found in '{composeFile}'.");
        }

        return match.Groups["image"].Value;
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Choco.slnx")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new InvalidOperationException($"No 'Choco.slnx' found above '{AppContext.BaseDirectory}'.");
        }

        return directory.FullName;
    }

    [GeneratedRegex(@"^\s*image:\s*(?<image>postgres:\S+)\s*$", RegexOptions.Multiline)]
    private static partial Regex ComposePostgresImageRegex();
}
