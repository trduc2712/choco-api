# Secrets

| Setting | Value |
| --- | --- |
| `Clerk:SecretKey` | The Clerk Backend API secret key, starting with `sk_test_` (development) or `sk_live_` (production) |
| `Clerk:WebhookSigningSecret` | The signing secret of the Clerk webhook endpoint, starting with `whsec_`; for local development, see [clerk-webhooks-local.md](clerk-webhooks-local.md) |
| `Storage:AccessKeyId` | The application key ID of the Backblaze B2 key for the environment. Terraform creates the key and sets it in the Render env group, so staging and production need no manual entry; local development uses the SeaweedFS key in `appsettings.Development.json`, see [object-storage-local.md](object-storage-local.md) |
| `Storage:SecretAccessKey` | The application key that goes with `Storage:AccessKeyId`, set by Terraform the same way |
| `Database:ConnectionString` | The PostgreSQL connection string, `Host=<host>;Port=<port>;Database=<database>;Username=<username>;Password=<password>` |
