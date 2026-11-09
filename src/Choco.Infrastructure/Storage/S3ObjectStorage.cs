using System.Net;

using Amazon.S3;
using Amazon.S3.Model;

using Choco.Application.Abstractions.Storage;
using Choco.Infrastructure.Options;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Choco.Infrastructure.Storage;

internal sealed partial class S3ObjectStorage(
    IAmazonS3 client,
    IOptions<StorageOptions> options,
    TimeProvider timeProvider,
    ILogger<S3ObjectStorage> logger) : IObjectStorage
{
    private readonly ILogger<S3ObjectStorage> _logger = logger;
    private readonly StorageOptions _options = options.Value;
    private readonly Protocol _protocol =
        new Uri(options.Value.ServiceUrl).Scheme == Uri.UriSchemeHttp ? Protocol.HTTP : Protocol.HTTPS;

    public async Task<Uri> CreateUploadUrlAsync(
        string key, string contentType, TimeSpan expiresIn, CancellationToken cancellationToken = default)
    {
        GetPreSignedUrlRequest request = new()
        {
            BucketName = _options.UploadsBucketName,
            Key = key,
            Verb = HttpVerb.PUT,
            Protocol = _protocol,
            ContentType = contentType,
            Expires = timeProvider.GetUtcNow().UtcDateTime.Add(expiresIn),
        };

        Uri url = new(await client.GetPreSignedURLAsync(request));
        LogUploadUrlCreated(StorageBucket.Uploads, key, expiresIn);

        return url;
    }

    public async Task<Uri> CreateDownloadUrlAsync(
        string key, TimeSpan expiresIn, CancellationToken cancellationToken = default)
    {
        GetPreSignedUrlRequest request = new()
        {
            BucketName = _options.MediaBucketName,
            Key = key,
            Verb = HttpVerb.GET,
            Protocol = _protocol,
            Expires = timeProvider.GetUtcNow().UtcDateTime.Add(expiresIn),
        };

        Uri url = new(await client.GetPreSignedURLAsync(request));
        LogDownloadUrlCreated(StorageBucket.Media, key, expiresIn);

        return url;
    }

    public async Task<bool> ExistsAsync(StorageBucket bucket, string key, CancellationToken cancellationToken = default)
    {
        try
        {
            await client.GetObjectMetadataAsync(BucketName(bucket), key, cancellationToken);
            LogObjectFound(bucket, key);

            return true;
        }
        catch (AmazonS3Exception exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            LogObjectNotFound(bucket, key);

            return false;
        }
    }

    public async Task<Stream?> GetAsync(StorageBucket bucket, string key, CancellationToken cancellationToken = default)
    {
        try
        {
            GetObjectResponse response = await client.GetObjectAsync(BucketName(bucket), key, cancellationToken);
            LogObjectRead(bucket, key);

            return response.ResponseStream;
        }
        catch (AmazonS3Exception exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            LogObjectNotFound(bucket, key);

            return null;
        }
    }

    public async Task PutAsync(
        StorageBucket bucket, string key, Stream content, string contentType,
        CancellationToken cancellationToken = default)
    {
        PutObjectRequest request = new()
        {
            BucketName = BucketName(bucket),
            Key = key,
            InputStream = content,
            ContentType = contentType,
        };

        await client.PutObjectAsync(request, cancellationToken);
        LogObjectWritten(bucket, key, contentType);
    }

    public async Task DeleteAsync(StorageBucket bucket, string key, CancellationToken cancellationToken = default)
    {
        await client.DeleteObjectAsync(BucketName(bucket), key, cancellationToken);
        LogObjectDeleted(bucket, key);
    }

    private string BucketName(StorageBucket bucket) => bucket switch
    {
        StorageBucket.Uploads => _options.UploadsBucketName,
        StorageBucket.Media => _options.MediaBucketName,
        _ => throw new ArgumentOutOfRangeException(nameof(bucket), bucket, null),
    };

    [LoggerMessage(Level = LogLevel.Debug, Message = "Created upload URL for {Bucket}/{Key}, valid for {ExpiresIn}")]
    private partial void LogUploadUrlCreated(StorageBucket bucket, string key, TimeSpan expiresIn);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Created download URL for {Bucket}/{Key}, valid for {ExpiresIn}")]
    private partial void LogDownloadUrlCreated(StorageBucket bucket, string key, TimeSpan expiresIn);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Object {Bucket}/{Key} exists")]
    private partial void LogObjectFound(StorageBucket bucket, string key);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Object {Bucket}/{Key} not found")]
    private partial void LogObjectNotFound(StorageBucket bucket, string key);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Read object {Bucket}/{Key}")]
    private partial void LogObjectRead(StorageBucket bucket, string key);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Wrote object {Bucket}/{Key} as {ContentType}")]
    private partial void LogObjectWritten(StorageBucket bucket, string key, string contentType);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Deleted object {Bucket}/{Key}")]
    private partial void LogObjectDeleted(StorageBucket bucket, string key);
}
