# 4. Apply Migrations in CI

Date: 2026-11-01

## Status

Accepted

## Context

The database schema is defined by EF Core migrations. Staging runs on Render
with a Neon PostgreSQL database, and Render deploys a commit on `main` once
all of its GitHub checks have passed. Every change merged into `main` has to
reach the staging database before the code that needs it starts serving
requests.

Applying migrations has to be repeatable and leave a log, because nobody
watches a deploy as it happens. A failed migration must stop the deploy
instead of leaving new code running against an old schema.

## Considered Options

- The API applies pending migrations itself when it starts.
- Render runs the migrations as a pre-deploy command.
- A CI job applies the migrations with an EF Core migration bundle, as one of
  the checks Render waits for.
- A person applies the migrations by hand before merging.

## Decision

choco-api applies migrations to staging in a CI job, before Render deploys.

- The job runs on every push to `main`, after the build, test and database
  documentation jobs have passed.
- It builds a self-contained migration bundle from the same commit Render
  will deploy and runs it against the staging database.
- The connection string comes from a secret of the GitHub `staging`
  environment.
- Render deploys only once this job has passed, because it waits for every
  check of the commit.
- The previous code keeps running on the new schema while the deploy
  finishes, so every migration stays compatible with it. How migrations are
  written for that is described in the `migration` skill.

Applying migrations at startup gives the running API the right to change the
schema, lets several instances race to apply the same migration, and turns a
failed migration into an API that does not start. A pre-deploy command runs
outside CI, so its log and its failure live in Render instead of next to the
checks of the commit. Applying them by hand is easy to forget and cannot be
repeated exactly.

## Consequences

Easier:

- A schema change reaches staging on every merge with no manual step.
- A failed migration stops the deploy, and its log sits in the CI run of the
  commit that caused it.
- The API does not change the schema at startup, so it can later run with a
  database role that cannot.

Harder:

- One more CI job can fail and hold back a deploy.
- A migration that fails halfway leaves the staging database in a partial
  state; nothing rolls it back.
- Building the bundle adds time to every deploy.
- A breaking schema change has to be split over two pull requests.
- Production needs its own way to apply migrations, which this ADR does not
  decide.
