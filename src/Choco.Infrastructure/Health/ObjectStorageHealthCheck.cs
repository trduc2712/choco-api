using Amazon.S3;
using Amazon.S3.Model;

using Choco.Infrastructure.Options;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Choco.Infrastructure.Health;

internal sealed class ObjectStorageHealthCheck(IAmazonS3 client, IOptions<StorageOptions> options) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        foreach (string bucketName in new[] { options.Value.UploadsBucketName, options.Value.MediaBucketName })
        {
            await client.ListObjectsV2Async(
                new ListObjectsV2Request { BucketName = bucketName, MaxKeys = 1 }, cancellationToken);
        }

        return HealthCheckResult.Healthy();
    }
}
