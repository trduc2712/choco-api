$ErrorActionPreference = 'Stop'
$env:NUKE_TELEMETRY_OPTOUT = '1'
dotnet run --project "$PSScriptRoot/build/_build.csproj" -- $args --root "$PSScriptRoot"
exit $LASTEXITCODE
