# 3. Use tbls for Database Documentation

Date: 2026-11-01

## Status

Accepted

## Context

choco-api needs documentation of its database: the tables, their columns,
keys and indexes, how they relate, and what each of them means.

The schema is defined by EF Core migrations and nowhere else. Documentation
written by hand is a second description of the same thing, and nothing forces
it to change when a migration is added, so sooner or later it is wrong.

## Considered Options

- Write the documentation by hand, for example as DBML or a Mermaid diagram.
- Generate it from the EF Core model.
- Generate it from a migrated database with tbls.
- Generate it from a migrated database with SchemaSpy.

## Decision

choco-api generates its database documentation from a migrated PostgreSQL
database with tbls, and commits the result.

- The output is Markdown with a Mermaid ER diagram in `docs/database/`, which
  GitHub renders. Nobody edits it by hand.
- Each table and column is described once, in its EF Core configuration. The
  migration stores the description as a PostgreSQL comment, and tbls reads it
  from there.
- CI applies all migrations to an empty database and fails when the committed
  documentation differs from it, or when a table or column has no
  description.
- tbls runs from a pinned Docker image, so it needs nothing installed besides
  Docker.

The EF Core model misses whatever a migration does in raw SQL; a migrated
database is what really exists. SchemaSpy also reads the database, but
produces HTML that GitHub does not render and needs Java.

## Consequences

Easier:

- The documentation cannot drift from the schema without CI failing.
- A schema change shows up as a readable Markdown diff in the pull request,
  next to the migration that caused it.
- The descriptions are also in the database itself, visible from any database
  client.

Harder:

- Every migration needs one more step: regenerating the documentation.
- Changing a description needs a migration, because the description is part
  of the schema.
- A table cannot be documented before its migration exists.
- Regenerating needs Docker and a local database with all migrations applied.
- CI has one more job that can fail.
