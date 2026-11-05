$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
& (Join-Path $PSScriptRoot 'Use-LocalSdk.ps1')
if (Test-Path Env:ConnectionStrings__AdSync) {
    throw 'Existe uma sobrescrita de conexão no processo. Remova ConnectionStrings__AdSync para usar exclusivamente os User Secrets existentes.'
}
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:Seed__Enabled = 'false'
$env:Preventivas__GeracaoAutomaticaHabilitada = 'false'
$env:Safety__ReadOnly = 'false'
dotnet run --project (Join-Path $root 'ADSync.Api') --no-launch-profile --urls http://localhost:5099
