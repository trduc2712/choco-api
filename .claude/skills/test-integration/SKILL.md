---
name: test-integration
description: Write or review integration tests for this API (in-process host over HTTP, real PostgreSQL in Testcontainers). Use when adding tests for an endpoint, webhook or use case, or when changing the integration test host.
---

# Integration Tests

An integration test runs the API in-process through a `WebApplicationFactory`, against real PostgreSQL in Testcontainers, with test doubles only for dependencies this project does not own.

## Workflow

1. Read the endpoint, its handler and `docs/test-data.md`.
2. Classify the dependencies (rule 1) and list the scenarios (rule 3) before writing code.
3. Write the tests and run them once. See each new test fail once by breaking the code it protects, then restore the code; a test that never failed may pass for the wrong reason. Run a new or changed test 5 times in a row, filtered to that test, when it depends on time, concurrency, background work or result order, or has been flaky before.
4. Run `./build.ps1 Check`.

## Scope and Entry Point

1. Classify every dependency of the use case before writing a test:
   - Managed (only this app uses it, e.g. its database): use the real one.
   - Unmanaged (other systems see its effects, e.g. an identity provider, email, a message bus): replace it at the edge and assert what goes out.
   - Non-deterministic (clock, background jobs, random values): make it fixed.
2. Enter through HTTP with the factory's `HttpClient`. Use MediatR `Send` only when HTTP cannot reach the case. Do not test repositories directly, except complex queries.
3. Cover each command endpoint with:
   - the longest happy path, touching every out-of-process dependency,
   - one 400 test proving validation is wired,
   - 401 and 403, one per kind of policy,
   - 404, including a soft-deleted row,
   - 409 for unique and concurrency conflicts,
   - idempotency, when the endpoint promises it.
4. Leave domain rules and individual validator rules to unit tests.

## Host and Fixtures

5. Test against PostgreSQL only. Never use `UseInMemoryDatabase`, SQLite or a mocked `DbSet`; treat any guide that does as outdated.
6. Pin the container image to the exact version production runs, and keep that tag in one file a dependency bot can update. Keep Ryuk on and do not use `WithReuse`.
7. Start one container per test run with an xUnit v3 assembly fixture. Give each test class its own database, cloned from a migrated template database, so classes run in parallel and tests inside a class run one at a time.
8. Configure the host once in the factory:
   - environment `Testing`, settings from in-memory configuration,
   - service replacements in `ConfigureTestServices`,
   - hosted services removed, background work run explicitly by the test,
   - `HttpClient` created with `AllowAutoRedirect = false`.
   Do not call `WithWebHostBuilder` inside individual tests.
9. Pass `TestContext.Current.CancellationToken` to every async call.
10. Send server logs to the test output, so a failure in CI shows what the server did.

## Database

11. Reset the data at the start of each test with Respawn. Do not clean up at the end, and do not wrap tests in a rolled-back transaction.
12. Apply migrations with `MigrateAsync` once, on the template database, through the app's own host so the DbContext gets its real dependencies. Close every pooled connection to the template before cloning it, and clone one database at a time. Never use `EnsureCreated`. Create the Respawner after migrating, and ignore `__EFMigrationsHistory` and tables seeded by `HasData`.
13. Arrange through the domain: create aggregates with their factory methods and save them with a DbContext helper in its own scope. Seed through the API only when that setup is the behavior under test.
14. Read persisted state through a new scope with `AsNoTracking`. Add `IgnoreQueryFilters` when checking soft-deleted rows.
15. Assert only rows the test created. Query with `ORDER BY` whenever the assertion depends on order.
16. Test unique indexes, including both sides of a filtered index, and concurrency conflicts on the real database. Assert the constraint name, not just that saving failed.
17. Compare timestamps read back from PostgreSQL at microsecond precision; it does not store .NET's 100 ns ticks.
18. Write test data by `docs/test-data.md`. Add an Object Mother or Test Data Builder only once several tests build the same object.

## Test Doubles and Time

19. Replace `TimeProvider` with one `FakeTimeProvider` singleton. Set it at the start of each test to a fixed instant on a whole second, and assert timestamps exactly. Use `BeCloseTo` only for values the test cannot control, such as a database `now()` default.
20. Authenticate with real JWTs signed by a test RSA key, and point JwtBearer at that key in `ConfigureTestServices` so the real handler, claim mapping and token validation run. Set `JwtBearerOptions.Configuration` with `Configure`, not `PostConfigure`: JwtBearer's own post-configure must see it, or it fetches metadata from the real authority. JwtBearer checks token lifetime against the system clock, so issue tokens from `DateTimeOffset.UtcNow`. Do not use a fake `AuthenticationHandler`. Cover:
    - no token → 401,
    - wrong signature, expired, wrong issuer, wrong audience or unauthorized party → 401,
    - valid token without permission → 403,
    - valid token → success, with the token's `sub` reaching the current-user abstraction.
21. Sign inbound webhooks with the provider's algorithm and a test secret, and send exactly the signed bytes. Give each test its own message id and timestamp. Cover a bad signature, a tampered body, missing headers, a timestamp outside the tolerance and a duplicate delivery of the same message id. Add out-of-order events once one entity has several event types.
22. Replace outgoing HTTP at the primary handler of `IHttpClientFactory` with a stub handler or WireMock.Net. Assert the requests sent and that nothing else was sent. Remove or configure resilience handlers on purpose, so retries do not hide failures or slow the test.
23. Run background work by calling the processor's single-batch method from the test. Assert that an outbox message is saved in the same transaction as the change that raised it. When waiting is unavoidable, poll with a timeout through one shared helper.

## Assertions

24. Assert, in this order: status code, response body contract, persisted state, outgoing side effects.
25. Read bodies with the app's JSON options. For errors, read `ProblemDetails` or `ValidationProblemDetails` and assert the status, the field keys in `errors`, the `code` extension and the `application/problem+json` content type.
26. Build request bodies as anonymous objects or raw JSON in the wire format, not with the app's request types, so a renamed property breaks the test.
27. Do not assert logs unless the log is a requirement, such as an audit or security event; then use `FakeLogCollector`.

## Naming and Structure

28. Name a test by the behavior in plain words, without the method name: `UserCreatedEvent_CreatesUser`, `DuplicateUsername_ReturnsConflict`.
29. Use one test class per endpoint or use case, in folders that mirror the API's features.
30. Separate Arrange, Act and Assert with blank lines. Keep the request under test visible in the test body; helpers may hide only setup that does not matter to the test.
31. Keep tests independent: no order between tests, no shared mutable or static state.

## Flakiness and CI

32. Never wait with `Thread.Sleep` or `Task.Delay`. Never use fixed ports, fixed file paths, unseeded random values or culture-dependent formatting. Use the wall clock only where a library reads it itself, such as webhook signature timestamps, and keep a margin around its tolerance.
33. Never retry failing tests silently. Quarantine a flaky test with a `Quarantined` trait that links an issue, and fix or delete it within a week.
34. Run integration tests on every pull request in a Linux CI job of their own, separate from unit tests, and publish the test report.
