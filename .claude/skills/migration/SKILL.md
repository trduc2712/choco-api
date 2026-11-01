---
name: migration
description: Change the database schema with an EF Core migration, or review one. Use when adding or changing an entity configuration, a table, column, index, constraint, comment or seed data, or when reviewing a generated migration.
---

# Database Migrations

The schema is defined by EF Core migrations and nowhere else. A migration merged into `main` is applied to staging by CI before the new code is deployed, so it has to be safe for the code that is still running.

## Workflow

1. Change the entity configuration in `Choco.Infrastructure/Persistence/Configurations/`.
2. Name the migration by `docs/migration-naming.md`, and ask the user before running `./build.ps1 MigrationAdd --migration-name <Name>`.
3. Review the generated migration by the rules below, and tell the user about every warning from rule 9.
4. Ask the user before running `./build.ps1 DbUp MigrationApply`, then run `./build.ps1 DbDocs`.
5. Run `./build.ps1 DbDocsCheck` and `./build.ps1 Check`.

## Configuration

1. Give every new table and column a description with `HasComment`; `tbls lint` in CI fails without one. The columns of `BaseEntity`, `IAuditable` and `ISoftDeletable` get theirs in `ChocoDbContext`, so do not repeat them.
2. Write a comment for a reader of the database: what the row or value means and where it comes from, not how the code uses it.
3. Take string lengths and other limits from the entity's constants (`User.UsernameMaxLength`), never from a literal.
4. Leave table, column, key and index names to the snake_case naming convention (`pk_users`, `ix_users_username`). Pass a name only for a table, with `ToTable`.

## Generated Migration

5. Read both `Up` and `Down`. `Down` undoes exactly what `Up` does.
6. A rename can be generated as a drop and an add, which loses the data. Replace it with `RenameColumn` or `RenameTable`.
7. Write what EF cannot generate, such as an extension, a trigger or a backfill, with `migrationBuilder.Sql(...)` in the same migration, with its reverse in `Down`.
8. Never edit `ChocoDbContextModelSnapshot.cs` or a `.Designer.cs` file by hand.
9. Warn the user before applying a migration that drops a table or column, narrows a type or length, or makes a column that already has rows required without a default or a backfill.

## Deploy Safety

10. While a deploy runs, the previous code runs on the new schema. A migration must not break it: no dropping or renaming a column or table the running code uses, and no new required column it does not write.
11. Make such a change in two pull requests (expand, then contract): the first adds the new shape and keeps the old one working, the second removes the old shape once no deployed code uses it.

## History

12. Never change a migration that is merged into `main`; staging has applied it. Fix it with a new migration.
13. A migration that exists only on the current branch may be removed and added again. Ask the user first when it is already applied to the local database, because removing it changes that database.
14. One logical change per migration (rule 9 in `docs/migration-naming.md`). Commit it together with the configuration change and the regenerated `docs/database`.

## Forbidden

15. Never run `DbReset`. When the local database is broken, ask the user to run it.
