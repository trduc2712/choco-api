# API Versioning

Client-facing endpoints require a version in the URL. Version 1 is the current
contract:

- `/api/v1/me`, `/api/v1/me/profile` and `/api/v1/me/posts`
- `/api/v1/posts` and `/api/v1/posts/{id}`
- `/api/v1/users/{userId}` and `/api/v1/users/{userId}/roles/{roleId}`
- `/api/v1/users/by-username/{username}`

The previous `/api/me`, `/api/posts` and `/api/users/...` routes are removed.
Clients must update their URLs; there is no default route or redirect. Query
parameters and headers do not select the API version.

Successful responses include `api-supported-versions: 1.0`. Creating a post
returns a `Location` URL containing the requested version; clients can retrieve
the resource at that URL with the same authentication.

## Errors

A URL without a version returns `404`. An unsupported URL version such as
`/api/v2/me/profile`, or a malformed version such as `/api/vinvalid/me/profile`,
also returns `404` with an `application/problem+json` body. These requests do not
run a use case. Unknown resources and unsupported versions both use `404` with
URL versioning, so clients must use a version listed in the documentation.

## Documentation and Other Endpoints

When API documentation is enabled, `/openapi/v1.json` describes version 1 and
Swagger UI at `/swagger` lists the supported version documents. Each document
uses concrete versioned URLs, so Try it out sends requests to that version.

The signed Clerk webhook at `/api/webhooks/clerk` is version-neutral and retains
its existing URL. The health endpoint remains `/health`.

## Adding a Version

Client-facing controllers declare their supported API versions explicitly with
`ApiVersion` and use the `api/v{version:apiVersion}/...` route pattern. Keep the
existing version's contract stable when adding a new version. The versioned API
explorer generates one OpenAPI document per declared version, and Swagger UI
discovers that list automatically. Resource links must carry the requested
version. XML documentation from the Contract assembly supplies schema
descriptions and examples for every version.

Implementation uses [ASP.NET API Versioning](https://github.com/dotnet/aspnet-api-versioning)
with its MVC, API Explorer and OpenAPI integration packages.
