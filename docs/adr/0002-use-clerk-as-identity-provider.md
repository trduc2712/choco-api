# 2. Use Clerk as the Identity Provider

Date: 2026-11-01

## Status

Accepted

## Context

The API needs to know who makes each request. Sign-up, sign-in, password
storage, email verification, social login, multi-factor authentication and
sessions are a large amount of security-sensitive work that has nothing to do
with a social network. choco-api should not build that, and needs a mature
system it can use right away.

## Considered Options

- Build authentication in choco-api with ASP.NET Core Identity.
- Self-host an open-source identity server, such as Keycloak.
- Use a hosted identity provider: Clerk.

## Decision

choco-api uses Clerk.

- The API is only a resource server: it validates the tokens Clerk issues,
  and issues no tokens and stores no credentials of its own.
- Code that knows about Clerk stays in the outer layers, behind interfaces the
  application layer owns.
- Users have their own id in the choco-api database and are linked to their
  Clerk user. How a request is mapped to a user is described in
  `docs/user-identity.md`.

Building authentication in choco-api is exactly the work to avoid.
Self-hosting avoids writing it but not running, securing and upgrading it.

## Consequences

Easier:

- choco-api does not build, secure or operate authentication.
- Mature features such as social login and multi-factor authentication are
  available without extra work.
- The API holds no sessions.

Harder:

- choco-api depends on Clerk for availability and pricing. If Clerk is down,
  nobody can sign in.
- Profile data lives in Clerk, so the local copy has to be kept in sync.
- The API cannot end a session itself. A token stays valid until it expires,
  so choco-api relies on Clerk's short token lifetime.
- Moving to another provider means moving the accounts out of Clerk, even
  though choco-api's own data does not depend on Clerk ids.
