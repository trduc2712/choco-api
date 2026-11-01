---
name: test-unit
description: Write or review unit tests for this API (domain model, SharedKernel primitives, validators, pipeline behaviors; in memory, no host or database). Use when adding or changing tests in tests/*.UnitTests, or when deciding whether a case belongs in a unit or an integration test.
---

# Unit Tests

A unit test runs one unit of behavior through its public API, with real domain objects, in one process, with no I/O, database, network or system clock.

## Workflow

1. Read the code under test and `docs/test-data.md`.
2. Decide that the case belongs here (rules 1–5) and list the scenarios before writing code.
3. Write the tests and run them once with `./build.ps1 Test --test-type unit`, narrowed with `--test-class` while working. Break the protected code once to see each new test fail, then restore it.
4. Run `./build.ps1 Check`.

## Scope

1. Unit-test aggregates, entity base types, value objects, pure domain services and policies, `Result`, `Error` and `ValidationError`, validators, pipeline behaviors, and mappings that branch.
2. Do not unit-test a handler that only loads, calls the domain and saves; the `test-integration` skill covers it. When a handler makes a real decision, move the decision into the domain or a pure policy and test it there.
3. Do not test DTOs, contract records, properties without logic, one-line mappings or framework behavior.
4. Test every business rule and every validator rule here; the `test-integration` skill leaves them to unit tests.
5. Do not test private members; extract complex private logic into its own type. Internal types are visible to `*.UnitTests` through `Directory.Build.props`. Do not add constructors, setters or public members only for tests, and do not use reflection on the code under test.

## Domain

6. Build aggregates through their factory methods and public methods only.
7. One Act per test. After a successful factory call, assert every property it sets. After a failed call on an existing aggregate, assert its state did not change.
8. A guard clause rejects input a validator should already have stopped, which is a bug: assert `Throw<ArgumentException>().WithParameterName(...)`, and `ThrowExactly` when the exact type is the contract. A rule a user can break returns a `Result` instead of throwing.
9. Domain events: call `ClearDomainEvents()` at the end of Arrange. Assert `DomainEvents.Should().ContainSingle()` with the event type and its data, taking ids from the aggregate. On a failure path, assert `DomainEvents.Should().BeEmpty()`.
10. Test `AggregateRoot` and `BaseEntity` behavior once, on the base types, through a private nested test entity; do not repeat it for each aggregate.
11. Errors: assert `IsFailure`, then `Error.Should().Be(UserErrors.X)` for a static error, or `Error.Code` and `Error.Type` for one built at runtime. Never assert `Description` or a message.
12. Group errors per aggregate (`UserErrors`). Once there are two such classes, keep one test that checks every `Code` is unique.
13. For a value object declared as a `record`, test only the normalization and validation in its factory, not the equality the compiler generates.

## Application

14. Validators: test the real validator with `TestValidate`. Keep one valid command as a `static readonly` field and change one property per test with `with`. Assert a rejection with `ShouldHaveValidationErrorFor(...).Only()` and a pass with `ShouldNotHaveAnyValidationErrors()`. Take limits from entity constants and test both sides of each. Never mock a validator; use `InlineValidator<T>` when a stub is needed.
15. Pipeline behaviors: call `Handle` directly with `next` as a lambda that takes the `CancellationToken`. Cover short-circuit (`next` not called, failure returned), pass-through (`next` called once, its result returned, asserted with `BeSameAs`), no validators, and errors merged from several validators. Leave proof that the behavior is registered to integration tests.
16. Time: aggregate methods take a `DateTimeOffset` parameter; domain tests pass a fixed UTC instant on a whole second. A type that holds a `TimeProvider` is tested with a `FakeTimeProvider` set to a fixed instant. Test both sides of each time boundary.

## Test Doubles

17. Use a stub for data coming in and never assert it was called. Use a spy only for calls going out to other systems (email, HTTP, a message bus), and assert those calls.
18. Never replace repositories, `IUnitOfWork`, `DbContext`, validators or domain objects with doubles.
19. Use no mocking library. Use lambdas, small stubs and spies written as private nested classes in the test class, `FakeTimeProvider` and `NullLogger<T>.Instance`. Add NSubstitute with its analyzers only when a hand-written spy grows or repeats, and only after the user agrees.
20. Do not assert logs unless the log is a requirement, such as an audit or security event; then use `FakeLogger<T>`.

## Naming and Structure

21. Name a test `Method_Scenario_Expected`, with the scenario and the result in business terms: `Create_WithBlankRequiredValue_ThrowsArgumentException`. Drop the scenario when there is none: `Success_ReturnsSuccessfulResultWithoutError`. This differs from integration tests on purpose: a unit test enters through a method.
22. Put tests in `tests/Choco.<Project>.UnitTests`, one `public sealed` class `<Type>Tests` per type under test, in folders and namespaces that mirror the source. Unit test projects never reference Infrastructure, EF Core or Testcontainers.
23. Separate Arrange, Act and Assert with blank lines. Keep the input under test visible in the test body; helpers may hide only setup that does not matter to the test.
24. No `if`, loops, `switch` or `try` in a test. Use `[Theory]` with `[InlineData]`, or `TheoryData<T>` with `MemberData(nameof(...))`, one value per class of input. Several assertions on one behavior are fine.
25. Write test data by `docs/test-data.md`. Add an Object Mother or Test Data Builder only once several tests build the same object.
26. Async tests return `Task` or `ValueTask`, pass `TestContext.Current.CancellationToken`, and await async assertions.
27. Use `Be` to compare with a type's own equality. `BeEquivalentTo` compares members and ignores collection order; add `WithStrictOrdering()` when order is the contract.
28. Keep tests independent: no order between tests, no shared mutable or static state. A `static readonly` immutable value is fine.

## Determinism and Quality

29. Never use `Thread.Sleep`, `Task.Delay`, unseeded random values, the system clock or the machine's culture. Never retry a failing unit test; a flaky unit test is a bug to fix at once.
30. Set no coverage or mutation threshold. Coverage and mutation testing are deferred; when added, they are reports only.
