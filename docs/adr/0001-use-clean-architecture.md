# 1. Use Clean Architecture

Date: 2026-11-01

## Status

Accepted

## Context

choco-api is the backend of a social network that will grow one feature at a
time over a long period. The code should stay easy to maintain, test and
extend as it grows: business rules should be testable without a database or a
web host, and the database, identity provider and frameworks should be
replaceable without rewriting those rules.

One person maintains the code, so structural rules cannot rely on code review
and have to be checked automatically.

## Considered Options

- A single layered project, with controllers, services and data access side by
  side.
- Vertical slice architecture, with code grouped by feature and each slice
  free to choose its own approach.
- Clean Architecture, with separate projects for domain, application,
  infrastructure and presentation, and dependencies pointing inward.

## Decision

choco-api uses Clean Architecture.

- Dependencies point inward. Domain and application code depend on no
  framework, database or external service; infrastructure and presentation
  depend on them, never the reverse.
- The application layer defines interfaces for what it needs from outside,
  and outer layers implement them.
- Each use case is one command or query with its own handler and validation.
  Expected failures are returned as results, not thrown.
- Architecture tests enforce the dependency rules in CI.

A single project gives no boundary a compiler or test can check, so business
rules leak into controllers and data access over time. Vertical slices keep a
feature's code together but leave the boundaries between slices to
discipline, which a single maintainer cannot review.

## Consequences

Easier:

- Business rules can be unit tested in memory.
- Infrastructure can be replaced behind the interfaces the application layer
  owns.
- A dependency that breaks the rules fails a test.
- Every feature has the same shape, so it is clear where new code goes.

Harder:

- Each feature needs more files, spread over several projects, even when it
  is trivial.
- Entities have to be mapped to DTOs.
- Dispatching use cases through a mediator adds indirection between an
  endpoint and its handler.
