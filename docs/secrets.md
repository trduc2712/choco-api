# Secrets

| Setting | Value |
| --- | --- |
| `Clerk:SecretKey` | The Clerk Backend API secret key, starting with `sk_test_` (development) or `sk_live_` (production) |
| `Clerk:WebhookSigningSecret` | The signing secret of the Clerk webhook endpoint, starting with `whsec_`; for local development, see [clerk-webhooks-local.md](clerk-webhooks-local.md) |
| `Database:ConnectionString` | The PostgreSQL connection string, `Host=<host>;Port=<port>;Database=<database>;Username=<username>;Password=<password>` |
