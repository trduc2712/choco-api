# 7. Refuse Suspended Users in the Application Pipeline

Date: 2026-11-12

## Status

Accepted

## Context

An administrator can suspend a user who breaks the rules. A suspended user
keeps reading, but everything that creates or changes data has to fail until an
administrator lifts the suspension. The refusal has to carry the reason, so the
user and the client can show it.

The rule touches every write the system accepts, today more than twenty and
more with each feature. A forgotten check is a hole: a suspended user keeps
posting. Some requests have no user behind them and must keep working, such as
creating a user from the identity provider or applying the configured
administrators. Reads must keep working. Other platforms limit an account in
three ways: a flag or a time stamp on the account that each action checks
(Discourse, Discord), a lighter level between a warning and a ban (Mastodon),
and, for Discord, a loss of permissions during the timeout.

[ADR 5](0005-use-role-based-access-control-with-roles-in-the-database.md) and
[ADR 6](0006-check-permissions-in-the-application-pipeline.md) already decide
what a user may do and where that is checked.

## Considered Options

- Refuse in a step of the application pipeline that runs before the handler,
  for every command except the ones marked as system requests.
- Refuse at the web layer with an authorization policy that lets only safe
  HTTP methods through for a suspended user.
- Turn each action of a member into a permission of the default role, and give
  a suspended user an empty set of permissions.
- Check the suspension in each handler, or in each domain method that changes
  data.

## Decision

choco-api refuses the commands of a suspended user in a step of the
application pipeline.

- The step runs after the permission check and before validation, for every
  command. A suspended caller gets a `403` with the code `User.Suspended`, and
  the response carries the reason and the time of the suspension.
- A command is refused unless it is marked as a system request. Only requests
  with no user behind them are marked, and a test fails when another one is
  added.
- Queries are never refused. The signed-in user reads their own suspension in
  `GET /me`.
- The step reads the suspension from the user row that the request already
  loads to know who the caller is, so it costs no extra query.
- A suspended administrator is refused too, because the step does not look at
  permissions.

How the rule works for a client and for an operator is in
[Authorization](../authorization.md).

The web policy was rejected because it only covers HTTP, as
[ADR 6](0006-check-permissions-in-the-application-pipeline.md) says of
endpoint policies, and because it reads the HTTP method instead of the meaning
of the request. Turning member actions into permissions was rejected for now:
it adds about fifteen permissions and an authorizer to almost every command, and
it lets an administrator remove posting from the default role by accident. It
stays the better fit if the system later needs finer limits than a full
suspension. Checks in each handler were rejected because they refuse nothing by
default and a new handler that forgets the check lets a suspended user write.

## Consequences

Easier:

- A command added later is refused for a suspended user without anyone
  remembering to do it.
- Every way into a command is covered, not only HTTP.
- The rule lives in one step and is unit-tested without a web host, like the
  permission check.
- A client gets one error code with the reason, on every write.

Harder:

- Every action that changes data can return `403`, so the OpenAPI document has to
  declare it on all of them, and a new action has to do the same.
- A request from a signed-in user now loads that user before it is validated, so
  an invalid request from a caller who does not exist yet creates the user first.
- Nothing can be allowed to a suspended user without a mark on its request, such
  as deleting their own account or taking back a report. Each such case needs a
  new decision.
- A suspension has no end date. An administrator has to lift it by hand.
- The step has to skip system requests before it asks who the caller is,
  because creating the caller is itself a system request; reversing the order
  loops.
