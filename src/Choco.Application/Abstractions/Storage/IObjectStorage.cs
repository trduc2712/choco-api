namespace Choco.Application.Abstractions.Storage;

public interface IObjectStorage
{
    Task<Uri> CreateUploadUrlAsync(
        string key, string contentType, TimeSpan expiresIn, CancellationToken cancellationToken = default);

    Task<Uri> CreateDownloadUrlAsync(
        string key, TimeSpan expiresIn, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(StorageBucket bucket, string key, CancellationToken cancellationToken = default);

    Task<Stream?> GetAsync(StorageBucket bucket, string key, CancellationToken cancellationToken = default);

    Task PutAsync(
        StorageBucket bucket, string key, Stream content, string contentType,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(StorageBucket bucket, string key, CancellationToken cancellationToken = default);
}
