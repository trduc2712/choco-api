---
name: use-case
description: Add or review a use case of this API, a command or query with its validator, handler and endpoint. Use when adding an action for API clients, when changing the flow of an existing use case, or when reviewing a handler or controller.
---

# Use Cases

A use case is one command (changes state) or query (reads state), handled by one handler and reached through one endpoint. `CreateUser` in `src/Choco.Application/Users/CreateUser/` is the reference implementation of a command.

This skill covers Contract, Application and Presentation. Domain code follows the `entity` skill, tests the `test-unit` and `test-integration` skills.

## Workflow

1. Read the aggregate the use case works on, and its repository.
2. Write, in this order: the command or query, its response, its validator, its authorizer when it needs a permission, its handler, its audit describer when the command is recorded, the endpoint.
3. Write validator tests by the `test-unit` skill and endpoint tests by the `test-integration` skill.
4. Check the OpenAPI document (rule 28).
5. Run `./build.ps1 Check`.

## Contract

1. Declare a command as `public sealed record <Verb><Noun>Command(...)` in `src/Choco.Contract/<Plural>/`, implementing `ICommand` or `ICommand<TResponse>`. Declare a query as `public sealed record Get<Noun>Query(...) : IQuery<TResponse>` in the same folder.
2. Declare a response as `public sealed record <Noun>Response(...)` in `src/Choco.Contract/<Plural>/`. It holds only what clients may see, in primitive or Contract types, never a domain type.
3. A command holds only the fields a client may send. The caller's id is never in a command; the handler gets it from `ICurrentUser`.

## Validator

4. Give every command or query that takes input an `internal sealed class <Name>Validator : AbstractValidator<<Name>>` next to its handler, where `<Name>` is the command or query (`CreateUserCommandValidator`). Take limits from the entity's constants.
5. Do not call the validator or register it: the validation pipeline runs it before the handler, and validators and handlers are registered by assembly scanning.

## Authorization

6. Give every command or query that needs a permission an `internal sealed class <Name>Authorizer : IAuthorizer<<Name>>` next to its handler (`AssignRoleCommandAuthorizer`). It returns the permission from the catalog in `Permission` and the scope, taken from the request's own data.
7. Do not call the authorizer or register it: the authorization pipeline checks it before validation, and authorizers are registered by assembly scanning.
8. Check a rule that depends on data the handler loads, such as the permissions of a role being assigned, in the handler through a domain method or `IPermissionChecker`; an authorizer only sees the request.
9. A request sent by the system rather than a user, such as a background job, has no authorizer.

## Handler

10. Put the handler in `src/Choco.Application/<Plural>/<UseCase>/` as `internal sealed class <Name>Handler` (`CreateUserCommandHandler`), implementing `ICommandHandler<...>` or `IQueryHandler<...>`, with its dependencies injected through the constructor.
11. A command handler only orchestrates: load the aggregate, call its methods, `Add` new aggregates, `IUnitOfWork.SaveChangesAsync`, return the result. A business decision belongs in the domain.
12. Return expected failures as a `Result` with an error from `<Name>Errors`; never throw for them.
13. When the use case needs the caller, call `ICurrentUser.GetIdAsync` before adding any entity, and return its failure as is; it may save the caller's user on the same unit of work.
14. Pass `TimeProvider.GetUtcNow()` to a domain method whose rule depends on time; never read `DateTimeOffset.UtcNow`.

## Query

15. Read through the repository, never through `ChocoDbContext`: Application does not depend on Infrastructure or EF Core.
16. Give the repository a read method that uses `AsNoTracking`, named `...ForReadAsync` (`GetByIdForReadAsync`). Keep the tracked `GetByIdAsync` for commands, which change and save what they load.
17. Map the aggregate to its response in the handler. A missing row returns a `NotFound` error from `<Name>Errors`.

## Endpoint

18. Put the endpoint in `src/Choco.Presentation/Controllers/<Plural>Controller.cs`, a `public sealed` class deriving from `ApiController`, routed at `api/<plural>`. It is `[Authorize]` through the base class; add `[AllowAnonymous]` only with a reason, such as a signed webhook.
19. Bind the body straight to the command with `[FromBody]`. When the route carries an id, merge it with `command with { Id = id }`, so the body cannot name another resource.
20. Send the command with `Sender.Send(command, cancellationToken)` and return `HandleFailure(result)` on failure. The status code comes from the error's `ErrorType`; never pick one by hand.
21. A `POST` that creates a resource has a create command returning `ICommand<<Noun>Response>`, and returns `CreatedAtAction(nameof(GetById), new { id = response.Id }, response)`. The route value names match the `GetById` parameters. Every create endpoint therefore comes with `GET api/<plural>/{id}`.
22. A query returns `Ok(response)`. A command without a response returns `NoContent()`. A webhook returns `Ok()`, because its sender only needs to know it was received.

## OpenAPI Documentation

Swagger shows what the XML doc comments and response attributes say, and nothing else. `references/controller.md` shows a documented controller and records.

23. Give every controller a `<summary>` and every action a `<summary>`, `<remarks>`, a `<param>` for each route and query parameter and for the body, and a `<response code="...">` for each status code. Write for a client of the API: what the endpoint does and when each status happens, without class names, exceptions or internal details.
24. Put in `<remarks>` what a client must know before calling: who may call it, whether it is idempotent, and what else changes, such as a resource it creates or removes.
25. Declare every status code the action can return with `[ProducesResponseType]`: the success with its response type, 400 with `typeof(ValidationProblemDetails)`, and other failures with `typeof(ProblemDetails)`. Include 401 for every `[Authorize]` action, 403 for every action whose request has an authorizer or whose handler can return a `Forbidden` error, and 404 for every route with an id. A `<response>` without a matching attribute is dropped from the document.
26. Document every command, query and response that an endpoint takes or returns: a `<summary>` on the record, a `<param>` for each positional parameter that says what it means, its format and its limits, and an `<example>` with the whole JSON body in its wire format. Write no doc comments on anything Swagger does not show; the compiler does not ask for them (CS1591 is off).
27. Write example values by `docs/test-data.md`: each value names its field and keeps its real format. Give a route or query parameter its example with `<param name="..." example="...">`.
28. When adding or changing an endpoint, run the app on an available port, open `/openapi/v1.json`, and check that each new operation has its summary, description, parameter descriptions, examples and every response code. Stop the app afterwards. If local configuration or the database prevents startup, report that limitation.

## Audit Log

A command that creates, changes or deletes data on behalf of a user is recorded in the audit log, unless it is listed in the exclusions of the test that checks this.

29. Give every recorded command an `internal sealed class <Name>AuditDescriber : IAuditDescriber<<Name>, <TResponse>>` next to its handler (`DeletePostCommandAuditDescriber`). It returns the `AuditAction`, the target type and id, the user who owns the target and a short detail. Add a value to `AuditAction` or `AuditTargetType` when none fits.
30. Do not call or register a describer, and do not write audit entries from a handler: the audit pipeline runs the describer after the handler succeeds and saves the entry in the same transaction, and describers are registered by assembly scanning.
31. Take what the describer needs from the response. When the response lacks it, as a delete does, read it through a repository method whose name says `IncludingDeleted`.
32. Report the owner of the target in `AffectedUserId` even when it is the caller; the pipeline clears it in that case.
33. Keep `Detail` to identifiers and short values such as a role name or a status. Never put content, tokens, email addresses or usernames in it.
