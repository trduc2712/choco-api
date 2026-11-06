# Repository instructions

Run build, test, format and database tasks through NUKE: `./build.ps1 <Target>` in PowerShell or `./build.sh <Target>` in Bash. Targets are in `build/Build.cs`; list them with `--help --no-logo`.

Run `./build.ps1 Check` before reporting a task as done.

Never run `DbReset`; ask the user to run it. Ask the user before running `MigrationAdd` or `MigrationApply`.

Name migrations by `docs/migration-naming.md`. Write test data by `docs/test-data.md`.

Always create a separate branch before committing. Never commit directly to `main`. Use a branch prefix that matches the change type, such as `docs/` for documentation changes.
