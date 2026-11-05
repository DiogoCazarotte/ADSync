param([ValidateSet('mvp','pwa','storage','preventive-prepare','preventive-check')][string]$Suite = 'mvp', [switch]$AllowDestructiveDemoTests)
$ErrorActionPreference = 'Stop'
if (!$AllowDestructiveDemoTests) { throw 'Testes demonstrativos desativados. Criam ordens e consomem estoque; exigem autorização explícita e banco isolado.' }
$runtime = Invoke-RestMethod -Uri 'http://localhost:5099/api/runtime'
if ($runtime.readOnly) { throw 'API somente leitura. Testes com gravação proibidos neste ambiente.' }
$env:ALLOW_DESTRUCTIVE_DEMO_TESTS = 'true'
$root = Split-Path $PSScriptRoot -Parent
$previousPassword = $env:DEMO_PASSWORD
# Existing demonstration password is read only into the child process environment.
if (!$env:DEMO_PASSWORD) {
    $secretFile = Join-Path $env:APPDATA 'Microsoft\UserSecrets\c6a39c55-2a48-4e31-bf5a-9996b0d92007\secrets.json'
    $settings = Get-Content $secretFile -Raw | ConvertFrom-Json
    $env:DEMO_PASSWORD = $settings.'Seed:SenhaDev'
}
if (!$env:DEMO_PASSWORD) { throw 'Defina DEMO_PASSWORD.' }
$env:DEMO_DATABASE = 'adsync_dev'
try {
    Push-Location $root
    if ($Suite -like 'preventive-*') {
        node tests/preventive-job.cjs ($Suite.Replace('preventive-', ''))
    } else {
        npm.cmd run "test:$Suite"
    }
    if ($LASTEXITCODE -ne 0) { throw 'Teste falhou. Consulte o resultado acima.' }
} finally {
    Pop-Location
    if ($previousPassword) { $env:DEMO_PASSWORD = $previousPassword }
    else { Remove-Item Env:DEMO_PASSWORD -ErrorAction SilentlyContinue }
}
