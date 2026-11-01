# CLAUDE.md

Run build, test, format and DB tasks through NUKE: `./build.ps1 <Target>` in PowerShell, `./build.sh <Target>` in Bash (targets in `build/Build.cs`, list them with `--help --no-logo`).
Run `./build.ps1 Check` before reporting a task as done.
Never run `DbReset`; ask the user to run it.
Always ask the user before running `MigrationAdd` or `MigrationApply`.
Name migrations by `docs/migration-naming.md`.
Write test data by `docs/test-data.md`.
