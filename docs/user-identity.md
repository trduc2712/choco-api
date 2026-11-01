# User Identity

How a request is mapped to a user of this system, and why.

## Identifiers

| Identifier | Owner | Where |
| --- | --- | --- |
| Clerk user id (`sub` claim, `user_...`) | Clerk | Access token; `users.identity_id` (unique) |
| User id (`Guid`) | This system | `users.id` and every foreign key to a user |

## Decisions

1. Users are keyed by their own `Guid`, and linked to Clerk by a unique `identity_id` column. Clerk's guides use the same shape: [Sync Clerk data to your app with webhooks](https://clerk.com/docs/guides/development/webhooks/syncing), [How to sync Clerk user data to your database](https://clerk.com/articles/how-to-sync-clerk-user-data-to-your-database).
2. The Clerk user id is not used as a primary or foreign key, so the schema does not depend on one identity provider, and development and production data are not tied to two Clerk instances' ids.
3. The user id is not written back to Clerk (`external_id`) or into a custom session claim. Clerk does not recommend it, and the first token after sign-up would not have the claim anyway.
4. Each request that needs the user id looks it up by `identity_id`, at most once per request.
5. The `user.created` webhook can arrive after the user's first request ([Part 2: race condition on user creation](https://clerk.com/articles/how-to-sync-clerk-user-data-to-your-database-2)). The API then reads the user from the Clerk Backend API and creates it, so clients never wait for the webhook.
6. A soft-deleted user, or one Clerk no longer has, gets `401 User.Deleted`: the account no longer exists, so the caller has to sign in again.
7. A user without a name in Clerk gets their username as display name. The rule lives in `User.Create`.

## Types

| Type | Layer | Answers |
| --- | --- | --- |
| `IUserContext` | Application, implemented in Presentation | Who is calling, by Clerk user id, from the token |
| `ICurrentUser` | Application | Which `users.id` the caller is; creates the user if needed |
| `IIdentityProvider` | Application, implemented in Infrastructure | What Clerk knows about a user |

Queries by `identity_id` include soft-deleted users (`...IncludingDeleted`): the unique index on `identity_id` keeps a deleted user's id, so treating them as missing would try to insert a duplicate.

## Known Gaps

### Sync

- `user.updated` is not handled. Username, name and avatar stay as they were when the user was created.
- `user.deleted` is not handled, so no code soft-deletes a user and `401 User.Deleted` is reached only when Clerk no longer has the user.
- Because of the two gaps above, a username stays taken in `users` after it changes or its account is deleted in Clerk. A new Clerk user who takes that username fails to be created: their first request returns 500 and their `user.created` webhook fails on every retry.
- A webhook and a first request creating the same user at the same moment hit the unique index on `identity_id`; one of them returns 500 and succeeds on retry.
- A webhook with invalid data returns 400, so Clerk retries it for about a day before giving up.

### Calls to Clerk

- A failing Clerk Backend API, including a 429 rate limit, returns 500 to a new user's first request instead of a retryable 503.
- The Clerk `HttpClient` has no timeout or resilience handler of its own, so a hanging Clerk keeps the request waiting for `HttpClient`'s default 100 seconds.
- A new user's first request takes about one extra second for the call to Clerk.

### Usage

- `ICurrentUser.GetIdAsync` may save changes, so handlers call it before adding entities.
- When Clerk returns a user without a username, the caller gets the `CreateUserCommand` validation error (400 on `Username`), even on an endpoint that has nothing to do with users.
