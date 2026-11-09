using System.Net;
using System.Text;

using Choco.Application.Abstractions.Storage;
using Choco.Infrastructure.Options;
using Choco.Infrastructure.Storage;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Choco.IntegrationTests.Storage;

public sealed class S3ObjectStorageTests : IDisposable
{
    private static readonly DateTimeOffset PastTime = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly StorageFixture _fixture;
    private readonly Amazon.S3.IAmazonS3 _client;
    private readonly HttpClient _httpClient = new(new HttpClientHandler { AllowAutoRedirect = false });

    public S3ObjectStorageTests(StorageFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _httpClient.Dispose();
    }

    [Fact]
    public async Task WrittenObject_CanBeRead()
    {
        S3ObjectStorage storage = CreateStorage(TimeProvider.System);

        await storage.PutAsync(
            StorageBucket.Media, "written-object", ToStream("content"), "image/webp", TestContext.Current.CancellationToken);
        await using Stream? stream = await storage.GetAsync(
            StorageBucket.Media, "written-object", TestContext.Current.CancellationToken);

        stream.Should().NotBeNull();
        (await ReadAsync(stream!)).Should().Be("content");
    }

    [Fact]
    public async Task WrittenObject_Exists()
    {
        S3ObjectStorage storage = CreateStorage(TimeProvider.System);
        await storage.PutAsync(
            StorageBucket.Media, "existing-object", ToStream("content"), "image/webp", TestContext.Current.CancellationToken);

        bool exists = await storage.ExistsAsync(
            StorageBucket.Media, "existing-object", TestContext.Current.CancellationToken);

        exists.Should().BeTrue();
    }

    [Fact]
    public async Task MissingObject_DoesNotExist()
    {
        S3ObjectStorage storage = CreateStorage(TimeProvider.System);

        bool exists = await storage.ExistsAsync(
            StorageBucket.Media, "missing-object", TestContext.Current.CancellationToken);

        exists.Should().BeFalse();
    }

    [Fact]
    public async Task MissingObject_IsReadAsNull()
    {
        S3ObjectStorage storage = CreateStorage(TimeProvider.System);

        Stream? stream = await storage.GetAsync(
            StorageBucket.Media, "unread-object", TestContext.Current.CancellationToken);

        stream.Should().BeNull();
    }

    [Fact]
    public async Task ObjectInOtherBucket_IsNotFound()
    {
        S3ObjectStorage storage = CreateStorage(TimeProvider.System);
        await storage.PutAsync(
            StorageBucket.Uploads, "uploaded-object", ToStream("content"), "image/webp", TestContext.Current.CancellationToken);

        bool exists = await storage.ExistsAsync(
            StorageBucket.Media, "uploaded-object", TestContext.Current.CancellationToken);

        exists.Should().BeFalse();
    }

    [Fact]
    public async Task DeletedObject_DoesNotExist()
    {
        S3ObjectStorage storage = CreateStorage(TimeProvider.System);
        await storage.PutAsync(
            StorageBucket.Media, "deleted-object", ToStream("content"), "image/webp", TestContext.Current.CancellationToken);

        await storage.DeleteAsync(StorageBucket.Media, "deleted-object", TestContext.Current.CancellationToken);
        bool exists = await storage.ExistsAsync(
            StorageBucket.Media, "deleted-object", TestContext.Current.CancellationToken);

        exists.Should().BeFalse();
    }

    [Fact]
    public async Task DeletingMissingObject_Succeeds()
    {
        S3ObjectStorage storage = CreateStorage(TimeProvider.System);

        Func<Task> act = () => storage.DeleteAsync(
            StorageBucket.Media, "never-written-object", TestContext.Current.CancellationToken);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task UploadUrl_WritesToUploadsBucket()
    {
        S3ObjectStorage storage = CreateStorage(TimeProvider.System);
        Uri url = await storage.CreateUploadUrlAsync(
            "url-uploaded-object", "image/webp", TimeSpan.FromMinutes(10), TestContext.Current.CancellationToken);
        using ByteArrayContent body = CreateBody("content", "image/webp");

        using HttpResponseMessage response = await _httpClient.PutAsync(url, body, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await storage.ExistsAsync(StorageBucket.Uploads, "url-uploaded-object", TestContext.Current.CancellationToken))
            .Should().BeTrue();
        (await storage.ExistsAsync(StorageBucket.Media, "url-uploaded-object", TestContext.Current.CancellationToken))
            .Should().BeFalse();
    }

    [Fact]
    public async Task UploadUrl_WithOtherContentType_IsRejected()
    {
        S3ObjectStorage storage = CreateStorage(TimeProvider.System);
        Uri url = await storage.CreateUploadUrlAsync(
            "rejected-upload-object", "image/webp", TimeSpan.FromMinutes(10), TestContext.Current.CancellationToken);
        using ByteArrayContent body = CreateBody("content", "text/plain");

        using HttpResponseMessage response = await _httpClient.PutAsync(url, body, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DownloadUrl_ReadsFromMediaBucket()
    {
        S3ObjectStorage storage = CreateStorage(TimeProvider.System);
        await storage.PutAsync(
            StorageBucket.Media, "downloaded-object", ToStream("content"), "image/webp", TestContext.Current.CancellationToken);
        Uri url = await storage.CreateDownloadUrlAsync(
            "downloaded-object", TimeSpan.FromMinutes(10), TestContext.Current.CancellationToken);

        using HttpResponseMessage response = await _httpClient.GetAsync(url, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().Be("content");
    }

    [Fact]
    public async Task ExpiredDownloadUrl_IsRejected()
    {
        S3ObjectStorage storage = CreateStorage(new FakeTimeProvider(PastTime));
        await storage.PutAsync(
            StorageBucket.Media, "expired-url-object", ToStream("content"), "image/webp", TestContext.Current.CancellationToken);
        Uri url = await storage.CreateDownloadUrlAsync(
            "expired-url-object", TimeSpan.FromMinutes(10), TestContext.Current.CancellationToken);

        using HttpResponseMessage response = await _httpClient.GetAsync(url, TestContext.Current.CancellationToken);

        response.IsSuccessStatusCode.Should().BeFalse();
    }

    [Fact]
    public async Task UnsignedRequest_IsRejected()
    {
        Uri url = new($"{_fixture.Options.ServiceUrl}/{_fixture.Options.MediaBucketName}/unsigned-object");

        using HttpResponseMessage response = await _httpClient.GetAsync(url, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private S3ObjectStorage CreateStorage(TimeProvider timeProvider)
    {
        return new S3ObjectStorage(
            _client, Options.Create(_fixture.Options), timeProvider, NullLogger<S3ObjectStorage>.Instance);
    }

    private static ByteArrayContent CreateBody(string content, string contentType)
    {
        ByteArrayContent body = new(Encoding.UTF8.GetBytes(content));
        body.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);

        return body;
    }

    private static MemoryStream ToStream(string content)
    {
        return new MemoryStream(Encoding.UTF8.GetBytes(content));
    }

    private static async Task<string> ReadAsync(Stream stream)
    {
        using StreamReader reader = new(stream);

        return await reader.ReadToEndAsync(TestContext.Current.CancellationToken);
    }
}
