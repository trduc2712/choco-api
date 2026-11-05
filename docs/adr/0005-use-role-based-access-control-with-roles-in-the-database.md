# 5. Use Role-Based Access Control with Roles in the Database

Date: 2026-11-04

## Status

Accepted

## Context

Every signed-in user can call every endpoint. Moderating content, managing
other users and any staff-only action need a way to say that one user may do
more than another.

Today only an administrator role is needed, but more roles will come. A user
can hold several roles. Roles, and what each role may do, have to change while
the system runs, without a deploy. Communities are planned, and a user will
then hold roles inside one community that do not apply anywhere else, the way
a group admin on other social networks can only manage their own group.

[ADR 2](0002-use-clerk-as-identity-provider.md) and `docs/user-identity.md`
keep choco-api's own data out of Clerk, so that the schema does not depend on
one identity provider and a user's first token works before any webhook
arrives.

## Considered Options

- Store each user's roles in the identity provider's user metadata and read
  them from a claim in the session token.
- Use the identity provider's organizations, with their roles and
  permissions.
- Keep roles and the permissions of each role in code, and store only which
  roles each user holds in the database.
- Keep a catalog of permissions in code, and store roles, the permissions of
  each role and the roles of each user as data in the database.

## Decision

choco-api uses role-based access control, with roles stored as data in its
own database.

- Code checks permissions, never role names. A role is a named set of
  permissions, so a new role needs no code change.
- Permissions are a fixed catalog in code, because each permission only means
  something where code checks it. A new permission needs a code change.
- Roles and the permissions of each role are data and can change without a
  deploy.
- A user can hold several roles and has the permissions of all of them.
- A default member role applies to every signed-in user without being
  assigned to each one. Its permissions are data like any other role's.
- Each role assignment records who made it and when.
- A permission check always names the scope it applies to. The platform is
  the only scope today; community roles, a default role per community and the
  rule that combines platform and community roles come with communities.
- The first administrator of an environment is set from configuration.
- A rule that depends on who owns a resource, such as only the author editing
  a post, belongs to the domain model, not to roles.

Roles in the session token do not fit roles per community, which would make
every token carry a user's roles in all their communities, and they put
choco-api's data in Clerk, against ADR 2. The identity provider's
organizations model separate businesses, not communities inside one social
network, and would tie the data model to Clerk. Roles fixed in code cannot
change while the system runs.

## Consequences

Easier:

- A new role, or a change to what a role may do, needs no deploy.
- Code that checks a permission does not change when roles change.
- Questions such as which users are administrators are plain database
  queries, with foreign keys and transactions around the data.
- Removing a role takes effect on the user's next request.
- Community roles can be added without changing the code that calls the
  permission check.

Harder:

- Every request that needs a permission reads the user's roles from the
  database, while a claim in the token would cost nothing.
- Endpoints that create roles or change their permissions need rules against
  privilege escalation: a user grants only permissions they hold, does not
  change their own roles, and cannot remove the system roles or the last
  administrator.
- Each environment needs its first administrator set from configuration,
  which is one more piece of setup that has to be tested.
- A permission is defined in code but granted in data, so a role can be given
  a permission that no deployed code checks yet, or keep one that code no
  longer has.
