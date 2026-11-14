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
8. Clerk is the source of truth for username, name and avatar. `user.updated` changes them and `user.deleted` soft-deletes the user, which frees the username (the unique index on `username` ignores deleted users).
9. Events can arrive late, twice or out of order, so `users.identity_updated_at` stores the Clerk update time of the last event or Clerk response that wrote the profile. An event with the same or an earlier time is ignored. An event for a user the API does not know is acknowledged without a change.
10. If a `user.updated` event gives a username that another user still holds, for example a user took the name just released by a rename whose event has not arrived yet, the webhook returns 409. Clerk retries the event, and it succeeds once the other user's event has been applied.

## Types

| Type | Layer | Answers |
| --- | --- | --- |
| `IUserContext` | Application, implemented in Presentation | Who is calling, by Clerk user id, from the token |
| `ICurrentUser` | Application | Which `users.id` the caller is; creates the user if needed |
| `IIdentityProvider` | Application, implemented in Infrastructure | What Clerk knows about a user |

Queries by `identity_id` include soft-deleted users (`...IncludingDeleted`): the unique index on `identity_id` keeps a deleted user's id, so treating them as missing would try to insert a duplicate.

## Profile edits

- `PUT /api/v1/me/profile` replaces the caller's username, first name and last name. The display name is the first and last names joined with a space. An omitted last name clears it.
- `POST /api/v1/me/profile/avatar` accepts an image in the multipart field named `image`, up to 10 MB. `DELETE` on the same path clears the avatar.
- The API writes each edit to Clerk first and returns the updated local profile after saving it. If Clerk accepts an edit and the local save fails, the caller can retry the edit.

## Known Gaps

### Sync

- `PUT /api/v1/me/profile` and the avatar endpoints write the local copy directly and do not move `identity_updated_at`. The `user.updated` event of the same edit then applies the same values again, which changes nothing. Routing these edits through the sync path is a separate issue.
- A webhook with invalid data returns 400, so Clerk retries it for about a day before giving up.

### Calls to Clerk

- A failing Clerk Backend API, including a 429 rate limit, returns 500 to a new user's first request instead of a retryable 503.
- The Clerk `HttpClient` has no timeout or resilience handler of its own, so a hanging Clerk keeps the request waiting for `HttpClient`'s default 100 seconds.
- A new user's first request takes about one extra second for the call to Clerk.

### Usage

- `ICurrentUser.GetIdAsync` may save changes, so handlers call it before adding entities.
- When Clerk returns a user without a username, the caller gets the `CreateUserCommand` validation error (400 on `Username`), even on an endpoint that has nothing to do with users.
