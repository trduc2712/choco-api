using System.Net;
using System.Text.RegularExpressions;

using Amazon.Runtime;
using Amazon.S3;

using Choco.Infrastructure.Options;
using Choco.IntegrationTests.Storage;

using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

[assembly: AssemblyFixture(typeof(StorageFixture))]

namespace Choco.IntegrationTests.Storage;

public sealed partial class StorageFixture : IAsyncLifetime
{
    private const int S3Port = 8333;
    private const string AccessKeyId = "access-key-id";
    private const string SecretAccessKey = "secret-access-key";

    private readonly IContainer _container = new ContainerBuilder(FindComposeImage())
        .WithPortBinding(S3Port, true)
        .WithEnvironment("AWS_ACCESS_KEY_ID", AccessKeyId)
        .WithEnvironment("AWS_SECRET_ACCESS_KEY", SecretAccessKey)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request => request
            .ForPort(S3Port)
            .ForStatusCode(HttpStatusCode.Forbidden)))
        .Build();

    internal StorageOptions Options { get; private set; } = new();

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync(TestContext.Current.CancellationToken);

        Options = new StorageOptions
        {
            ServiceUrl = $"http://{_container.Hostname}:{_container.GetMappedPublicPort(S3Port)}",
            Region = "us-east-1",
            UploadsBucketName = "uploads-bucket",
            MediaBucketName = "media-bucket",
            AccessKeyId = AccessKeyId,
            SecretAccessKey = SecretAccessKey,
        };

        using IAmazonS3 client = CreateClient();
        await client.PutBucketAsync(Options.UploadsBucketName, TestContext.Current.CancellationToken);
        await client.PutBucketAsync(Options.MediaBucketName, TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        return _container.DisposeAsync();
    }

    internal IAmazonS3 CreateClient()
    {
        AmazonS3Config config = new()
        {
            ServiceURL = Options.ServiceUrl,
            AuthenticationRegion = Options.Region,
            ForcePathStyle = true,
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
        };

        return new AmazonS3Client(new BasicAWSCredentials(Options.AccessKeyId, Options.SecretAccessKey), config);
    }

    private static string FindComposeImage()
    {
        string composeFile = Path.Combine(FindRepositoryRoot(), "compose.yaml");
        Match match = ComposeImageRegex().Match(File.ReadAllText(composeFile));

        if (!match.Success)
        {
            throw new InvalidOperationException($"No 'image: chrislusf/seaweedfs:<tag>' line found in '{composeFile}'.");
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

    [GeneratedRegex(@"^\s*image:\s*(?<image>chrislusf/seaweedfs:\S+)\s*$", RegexOptions.Multiline)]
    private static partial Regex ComposeImageRegex();
}
