param([switch]$NoOpen)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$sdk = Join-Path $root '.local-demo/dotnet/dotnet.exe'
if (!(Test-Path -LiteralPath $sdk)) { $sdk = (Get-Command dotnet.exe).Source }
$state = Join-Path $root '.local-demo/pwa'
$release = Join-Path $state ('releases/' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force $release | Out-Null
# Cada publicação tem pasta própria. O servidor só a usa depois de terminar.
& $sdk publish (Join-Path $root 'ADSync.Client') -c Release --no-restore -o $release
if ($LASTEXITCODE -ne 0) { throw 'Falha ao publicar a PWA; a versão anterior foi preservada.' }
$metadata = Join-Path $state 'current.json'
$temporary = Join-Path $state ([Guid]::NewGuid().ToString('N') + '.json')
@{ root = (Join-Path $release 'wwwroot') } | ConvertTo-Json | Set-Content -Encoding UTF8 -LiteralPath $temporary
if (Test-Path -LiteralPath $metadata) {
    $backup = Join-Path $state ([Guid]::NewGuid().ToString('N') + '.previous.json')
    [IO.File]::Replace($temporary, $metadata, $backup)
}
else { Move-Item -LiteralPath $temporary -Destination $metadata }
$running = $null
try { $running = Invoke-RestMethod 'http://127.0.0.1:5180/_pwa/status' -TimeoutSec 2 } catch { }
if ($running -and $running.app -ne 'NexServicePwa') { throw 'Porta 5180 ocupada por outra aplicação.' }
if (!$running) {
    $probe = New-Object Net.Sockets.TcpClient
    try { $probe.Connect('127.0.0.1', 5180); throw 'Porta 5180 ocupada por outra aplicação.' }
    catch [Net.Sockets.SocketException] { }
    finally { $probe.Dispose() }
    $serverArgs = @(('"' + (Join-Path $root 'tools/serve-pwa.cjs') + '"'), '--state', ('"' + $metadata + '"'))
    $server = Start-Process -FilePath (Get-Command node.exe).Source -ArgumentList $serverArgs -WorkingDirectory $root -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $state 'server.log') -RedirectStandardError (Join-Path $state 'server-error.log')
    $server.Id | Set-Content -LiteralPath (Join-Path $state 'server.pid')
}
$api = $null
try { $api = Invoke-RestMethod 'http://localhost:5099/api/runtime' -TimeoutSec 2 } catch { }
if (!$api) { Write-Host 'PWA publicada. Inicie a API pelo Visual Studio para fazer login e sincronizar.' }
elseif (!$api.technicalOfflineEnabled) { Write-Host 'PWA publicada. Reinicie a API pelo Visual Studio para carregar a versão com atendimento offline.' }
Write-Host 'PWA: http://127.0.0.1:5180 — após login, os atendimentos são preparados automaticamente.'
Write-Host 'Para instalar, use Instalar NexService ou a opção do menu do navegador.'
if (!$NoOpen) { Start-Process 'http://127.0.0.1:5180' }
