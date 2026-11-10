$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$scratch = Join-Path $projectRoot ('.local-demo/docker-env-check-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $scratch | Out-Null
$source = Join-Path $scratch 'secrets.json'
$destination = Join-Path $scratch '.env.docker'
$connection = 'Host=127.0.0.1;Database=fixture;Username=test;Password="dollar$hash#back\slash"'
$jwt = 'fixture-jwt-with-$-and-#-and-at-least-32-characters'
@{ 'ConnectionStrings:AdSync' = $connection; 'Jwt:Key' = $jwt } | ConvertTo-Json | Set-Content -Encoding UTF8 -LiteralPath $source
& (Join-Path $projectRoot 'tools/Initialize-DockerEnv.ps1') -SecretsPath $source -OutputPath $destination
$content = [IO.File]::ReadAllText($destination)
if (!$content.Contains("ConnectionStrings__AdSync=$connection`n") -or !$content.Contains("Jwt__Key=$jwt`n")) { throw 'Valores especiais não foram preservados.' }
$bytes = [IO.File]::ReadAllBytes($destination)
if ($bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) { throw 'O arquivo não deve conter BOM.' }
$refused = $false
try { & (Join-Path $projectRoot 'tools/Initialize-DockerEnv.ps1') -SecretsPath $source -OutputPath $destination }
catch { $refused = $true }
if (!$refused -or [IO.File]::ReadAllText($destination) -cne $content) { throw 'Configuração existente não foi preservada.' }
$nestedSource = Join-Path $scratch 'nested.json'
@{ ConnectionStrings = @{ AdSync = $connection }; Jwt = @{ Key = $jwt } } | ConvertTo-Json | Set-Content -Encoding UTF8 -LiteralPath $nestedSource
$nestedDestination = Join-Path $scratch 'nested.env'
& (Join-Path $projectRoot 'tools/Initialize-DockerEnv.ps1') -SecretsPath $nestedSource -OutputPath $nestedDestination
if ([IO.File]::ReadAllText($nestedDestination) -cne $content) { throw 'Secrets aninhados não foram lidos corretamente.' }
$invalidSource = Join-Path $scratch 'invalid.json'
@{ 'ConnectionStrings:AdSync' = $connection; 'Jwt:Key' = 'curta' } | ConvertTo-Json | Set-Content -Encoding UTF8 -LiteralPath $invalidSource
$refused = $false
try { & (Join-Path $projectRoot 'tools/Initialize-DockerEnv.ps1') -SecretsPath $invalidSource -OutputPath (Join-Path $scratch 'invalid.env') }
catch { $refused = $true }
if (!$refused -or (Test-Path (Join-Path $scratch 'invalid.env'))) { throw 'Chave curta foi aceita.' }
Write-Host 'PASS: valores especiais preservados, UTF-8 sem BOM, arquivo existente protegido, secrets planos/aninhados e chave inválida recusada. Apenas credenciais fictícias.'
