# Object Storage on a Local Machine

The API reads and writes files through an S3-compatible service. Staging and production use Backblaze B2; a local machine uses [SeaweedFS](https://github.com/seaweedfs/seaweedfs) in Docker. How the buckets and keys are named is in [object-storage.md](https://github.com/trduc2712/choco-infrastructure/blob/main/docs/object-storage.md) in the choco-infrastructure repository.

## Prerequisites

- Docker.

## Start

```bash
./build.ps1 StorageUp
```

The target starts SeaweedFS on `http://localhost:8333` and creates the two buckets, `trduc2712-choco-development-uploads` and `trduc2712-choco-development-media`. It is safe to run again.

The API reads the `Storage` section of `appsettings.Development.json`, which already points at these buckets. The API does not start without that section.

## Stop

```bash
./build.ps1 DbDown
```

This stops every Docker service of the project and keeps the data volumes.

## Settings

| Setting | Meaning |
| --- | --- |
| `Storage:ServiceUrl` | The address of the S3 API, an absolute `http` or `https` URL |
| `Storage:Region` | The signing region; SeaweedFS accepts any value |
| `Storage:UploadsBucketName` | The private bucket that receives uploads, 6 to 63 lowercase letters, digits and hyphens |
| `Storage:MediaBucketName` | The private bucket that holds processed media; it must differ from the uploads bucket |
| `Storage:AccessKeyId`, `Storage:SecretAccessKey` | The key the API signs requests with, see [secrets.md](secrets.md) |

## Differences from Backblaze B2

- `DeleteObject` removes the file at once. In B2 it only hides the file until the bucket's lifecycle rule deletes it.
- The status code of an expired signed URL differs from AWS S3 and B2.
- Lifecycle rules and CORS are not configured locally.

The integration tests start their own SeaweedFS container from the image in `compose.yaml` and do not use this one.
