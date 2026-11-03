---
name: entity
description: Add or review a domain aggregate or entity and its persistence (repository, EF Core configuration). Use when adding a new domain concept, when adding behavior or properties to an existing aggregate, or when reviewing domain model code.
---

# Domain Entities

An aggregate is a cluster of domain objects saved and loaded as one, with one root that guards its rules. `User` in `src/Choco.Domain/Users/` is the reference implementation.

This skill covers the domain and persistence code. Migrations follow the `migration` skill, tests the `test-unit` skill, and commands, handlers and endpoints the `use-case` skill.

## Workflow

1. Ask the user what the concept is: its own aggregate or part of another, its invariants, whether it is audited or soft-deleted, and which events it raises.
2. Write, in this order: the domain class, its errors, its events, the repository interface, the EF Core configuration, the repository implementation and its registration.
3. Write unit tests by the `test-unit` skill, then add the migration by the `migration` skill.
4. Run `./build.ps1 Check`.

## Location

1. Put each aggregate in its own folder, `src/Choco.Domain/<Plural>/`, holding `<Name>.cs`, `<Name>Errors.cs`, `I<Name>Repository.cs` and its events. Child entities of the aggregate live in the same folder.
2. Put the configuration in `src/Choco.Infrastructure/Persistence/Configurations/<Name>Configuration.cs` and the repository in `src/Choco.Infrastructure/Persistence/Repositories/<Name>Repository.cs`.

## Shape

3. Make the class `sealed`. An aggregate root derives from `AggregateRoot`, a child entity from `BaseEntity`.
4. Give it a `private` parameterless constructor for EF Core and a `private` constructor that sets every property. Create instances only through `public static <Name> Create(...)`.
5. Every property has a `private set`. Change state only through methods named for what happens in the domain (`<Verb>`), not setters named after properties (`Set<Property>`).
6. Generate the id in `Create` with `Guid.NewGuid()`, as `User` does.

## Capabilities

7. Implement `IAuditable` and `ISoftDeletable` when the concept needs them, instead of a base class. Interceptors set their values; domain code never sets `CreatedAt`, `ModifiedAt`, `IsDeleted` or `DeletedAt`.
8. A method whose rule depends on time takes a `DateTimeOffset now` parameter; it never reads a clock.

## Invariants

9. Throw with a guard clause (`ArgumentException.ThrowIfNullOrWhiteSpace`, `ArgumentOutOfRangeException.ThrowIfGreaterThan`) for input a validator should already have stopped; reaching it is a bug.
10. Return a `Result` with an error from `<Name>Errors` for a rule a user can break.
11. Apply business defaults inside `Create` (a missing display name becomes the username), not in handlers.
12. Declare lengths and other limits as `public const` on the entity (`UsernameMaxLength`). Validators, EF Core configuration and tests use these constants.

## Errors

13. Declare errors as `public static readonly Error` fields in `public static class <Name>Errors`. Build an error that needs runtime data with a `public static Error` method.
14. Give each error the code `<Name>.<Reason>` and the `Error` factory whose type fits (`NotFound`, `Conflict`, `Forbidden`, ...); the type decides the HTTP status.

## Relationships

15. Reference another aggregate by its id only (`<Other>Id`), with no navigation property to it. Where one aggregate ends and the next begins is the user's decision (Workflow step 1).
16. Change a child entity only through methods on its aggregate root; expose child collections as `IReadOnlyCollection<T>` over a private list.

## Domain Events

17. Declare an event as `public sealed record <Name><PastTenseVerb>(...) : IDomainEvent` carrying ids and the data a handler needs, not the aggregate.
18. Raise it with `RaiseDomainEvent` inside the method that causes it, after the state has changed and only on success.

## Repository

19. Derive the interface from `IRepository<T>` and add only the queries a use case needs. A query that must see soft-deleted rows says so in its name (`...IncludingDeleted`).
20. Implement it as `internal sealed class <Name>Repository : Repository<T>, I<Name>Repository`, and register it with `AddScoped` in `src/Choco.Infrastructure/DependencyInjection.cs`.

## Configuration

21. Write the EF Core configuration by the `migration` skill: a comment on every table and column, limits from the entity's constants, names from the snake_case convention.
