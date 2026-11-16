# 8. Make the Database the Source of Truth for Messages

Date: 2026-11-16

## Status

Accepted

## Context

Users send private messages to each other, and anyone may message anyone. A
message must not be lost, shown twice or shown out of order, and the other
user should see it at once when they are online.

Real-time connections drop: a phone changes network, a server restarts, a
deploy replaces the process. A client also retries a send when it does not
hear back, so the same send can reach the server more than once. Two users can
send at the same moment, and two users can open their first conversation at
the same moment.

Production runs one instance of the API today. It has to keep working when it
runs on several, so no behavior may depend on what one process remembers.
Notifications are already pushed to connected users in real time after the
change is saved, and a failed push is only logged. The roadmap plans an outbox
for work that must not be lost, but it does not exist yet.

## Considered Options

- Deliver messages over the real-time connection and save them afterwards, so
  that delivery does not wait for the database.
- Save every message first, number it inside its conversation, and use the
  real-time connection only to tell clients that a new message exists.
- Save every message first and order it by its creation time, with the same
  real-time notice.
- Save every message first, and send the real-time notice from an outbox
  that retries until the notice is delivered.

## Decision

choco-api treats the database as the only source of truth for messages and the
real-time connection as a best-effort notice.

- A message is sent through a normal request. It is saved before anything is
  pushed, and the push happens only after the save has succeeded.
- Each conversation numbers its messages with a sequence that grows by one for
  every message. The database hands out the number inside the transaction that
  saves the message. Messages are ordered and paged by this number, never by
  time.
- The client creates an identifier for each send. A second send with the same
  identifier in the same conversation returns the message already saved and
  creates no new one.
- Two users have at most one direct conversation. The database enforces this
  with a unique key built from the two users, and the loser of a race reads the
  conversation that already exists.
- Each participant stores the number of the last message they have read.
  Unread counts are the difference between two numbers, not a count of rows.
- The real-time notice carries the whole message and its number. A client
  that sees a gap in the numbers, or that reconnects, asks the API for the
  messages after the last number it has. A failed push is logged and never
  fails the send.
- No behavior depends on memory inside one process. The code that pushes
  notices sits behind an abstraction, so a shared backplane can be added when a
  second instance runs, as its own decision.

Delivering before saving was rejected because a crash between the two loses
a message the sender was told was sent. Ordering by time was rejected because
two messages can share a time stamp and clocks differ between instances, so
the order would not be stable. An outbox was rejected for now: catching up
from the database already repairs a lost notice, and the outbox adds a table
and a worker that the system does not have yet. It stays the better fit if a
message must trigger work that cannot be repaired by reading.

## Consequences

Easier:

- A lost, delayed or duplicated notice cannot lose or duplicate a message,
  because the client repairs itself from the database.
- A retry is always safe, so clients can retry without care.
- Ordering and paging are exact and cheap, and unread counts need no scan.
- The same rules hold on one instance or on many, because the database
  decides order and uniqueness.

Harder:

- Every send does a write that takes a lock on its conversation, so a very hot
  conversation sends one message at a time.
- Clients must implement gap detection, catch-up and de-duplication; a client
  that only listens to notices will miss messages.
- The conversation keeps copies of data it could compute, such as the last
  message and the last number, and the code has to keep them correct inside
  the transaction.
- A second instance still needs a backplane before notices reach users
  connected to another instance; until it exists, such users see messages only
  when they catch up.
- Rate limits held in the memory of one process are per instance, so the
  effective limit grows with the number of instances.
