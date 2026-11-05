# 6. Check Permissions in the Application Pipeline

Date: 2026-11-04

## Status

Accepted

## Context

[ADR 5](0005-use-role-based-access-control-with-roles-in-the-database.md)
decides what a user may do. Each use case that needs a permission still has to
check it, in one consistent place.

Whether a call is allowed depends on data in the request: once communities
exist, the scope of a permission is the community the request names, which can
come from the route or from the body. Background jobs will run use cases with
no signed-in user. The OpenAPI document should list a 403 response only on
the endpoints that can return it, which today it lists on every endpoint.

## Considered Options

- Declare the required permission on each endpoint with an ASP.NET Core
  authorization policy, checked by the web framework before the action runs.
- Let each request to the application layer declare the permission and scope
  it needs, and check it in a pipeline step that runs before the handler.
- Declare the permission on each request, and have the pipeline step call
  ASP.NET Core authorization policies to check it.

## Decision

choco-api checks permissions in a pipeline step of the application layer.

- A request that needs a permission declares the permission and the scope it
  needs. The scope comes from the request's own data.
- The check runs before validation, so a caller without the permission gets a
  403 and learns nothing about how the request would have been validated.
- Requests sent by background jobs declare no permission. Work done by the
  system is its own use case, not an action of a user.
- The OpenAPI document lists a 403 response for the requests that declare a
  permission, and for no others.
- Rules about who owns a resource stay in the domain model, as ADR 5 says.

Endpoint policies are checked before the body is read, so every endpoint
whose scope comes from the body would need its own call to the authorization
service, and nothing outside HTTP would be checked. Calling ASP.NET Core
policies from the pipeline keeps a single place for the check but ties the
application layer to the web framework's authorization model for no gain over
checking permissions directly.

## Consequences

Easier:

- Every way into a use case is checked, not only HTTP.
- A scope taken from the body is as easy to check as one taken from the route.
- The check is unit-tested without a web host, like validation.
- A use case shows the permission it needs next to its own input.

Harder:

- The approach is not the one ASP.NET Core documents, so a reader who knows
  the framework has to learn it.
- The OpenAPI document needs custom code to find which endpoints can return
  403.
- A request that forgets to declare its permission is open to every
  signed-in user, the same risk as a forgotten attribute on an endpoint. An
  automated test has to catch it.
- A caller without the permission is still parsed and bound before being
  rejected, which costs a little more than rejecting the request at the
  endpoint.
