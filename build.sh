#!/usr/bin/env bash
set -eo pipefail
SCRIPT_DIR=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
export NUKE_TELEMETRY_OPTOUT=1
exec dotnet run --project "$SCRIPT_DIR/build/_build.csproj" -- "$@" --root "$SCRIPT_DIR"
