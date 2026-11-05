$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
& (Join-Path $PSScriptRoot 'Use-LocalSdk.ps1')
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet run --project (Join-Path $root 'ADSync.Client') --no-launch-profile --urls http://localhost:5125
