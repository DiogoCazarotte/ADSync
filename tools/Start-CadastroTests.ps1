$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Set-Location $root
$sdk = Join-Path $root '.local-demo/dotnet/dotnet.exe'
$pg = Join-Path $root '.local-demo/postgresql/pgsql/bin'
$data = Join-Path $root '.local-demo/pgdata-cadastros'
$state = Join-Path $root '.local-demo/cadastros'
New-Item -ItemType Directory -Force $state | Out-Null
foreach ($port in @(5100, 5126)) {
    $probe = New-Object Net.Sockets.TcpClient
    try {
        $probe.Connect('127.0.0.1', $port)
        throw "Porta $port ocupada. O ambiente de testes pode já estar aberto; consulte docs/CADASTROS-TESTES.md."
    } catch [Net.Sockets.SocketException] { }
    finally { $probe.Dispose() }
}
if (!(Test-Path $sdk) -or !(Test-Path "$pg/initdb.exe")) { throw 'SDK local ou PostgreSQL portátil ausente.' }
$env:DOTNET_ROOT = Split-Path $sdk
$env:PATH = "$env:DOTNET_ROOT;$env:PATH"
$credentialsPath = Join-Path $state 'credentials.json'
if (!(Test-Path $credentialsPath)) {
    @{ Password = ('Test!9a' + [Guid]::NewGuid().ToString('N')); JwtKey = ([Guid]::NewGuid().ToString('N') + [Guid]::NewGuid().ToString('N')); Email = 'paulo.gestor@sustennutri.com.br' } |
        ConvertTo-Json | Set-Content -Encoding UTF8 $credentialsPath
}
$credentials = Get-Content $credentialsPath -Raw | ConvertFrom-Json
if (!(Test-Path "$data/PG_VERSION")) {
    & "$pg/initdb.exe" -D $data -U adsync_test --encoding=UTF8 --locale=C --auth=trust
    if ($LASTEXITCODE -ne 0) { throw 'Falha ao inicializar PostgreSQL de testes.' }
}
& "$pg/pg_ctl.exe" -D $data status *> $null
if ($LASTEXITCODE -ne 0) {
    & "$pg/pg_ctl.exe" -D $data -l "$state/postgres.log" -o '-h 127.0.0.1 -p 55433' -w start
    if ($LASTEXITCODE -ne 0) { throw 'Falha ao iniciar PostgreSQL de testes na porta 55433.' }
}
$exists = & "$pg/psql.exe" -h 127.0.0.1 -p 55433 -U adsync_test -d postgres -Atc "SELECT 1 FROM pg_database WHERE datname = 'adsync_dev'"
if ($LASTEXITCODE -ne 0) { throw 'Falha ao consultar o cluster de testes.' }
if ($exists -ne '1') {
    & "$pg/psql.exe" -h 127.0.0.1 -p 55433 -U adsync_test -d postgres -v ON_ERROR_STOP=1 -c 'CREATE DATABASE adsync_dev'
    if ($LASTEXITCODE -ne 0) { throw 'Falha ao criar banco de testes.' }
}
# Environment overrides are scoped to this script and restored on exit.
$overrides = @{
    ConnectionStrings__AdSync = 'Host=127.0.0.1;Port=55433;Database=adsync_dev;Username=adsync_test';
    ASPNETCORE_ENVIRONMENT = 'Development'; Safety__ReadOnly = 'false'; Seed__Enabled = 'true';
    Seed__SenhaDev = $credentials.Password; Jwt__Key = $credentials.JwtKey;
    Preventivas__GeracaoAutomaticaHabilitada = 'false'; Cors__Origens__0 = 'http://127.0.0.1:5126'
}
$previous = @{}
try {
    foreach ($key in $overrides.Keys) { $previous[$key] = [Environment]::GetEnvironmentVariable($key, 'Process'); [Environment]::SetEnvironmentVariable($key, $overrides[$key], 'Process') }
    & $sdk run --project tools/DemoDatabase
    if ($LASTEXITCODE -ne 0) { throw 'Falha ao preparar o modelo no banco isolado.' }
    & $sdk build ADSync.Api --no-restore -o "$state/api"
    if ($LASTEXITCODE -ne 0) { throw 'Falha ao compilar a API de testes.' }
    & $sdk publish ADSync.Client -c Release --no-restore -o "$state/client"
    if ($LASTEXITCODE -ne 0) { throw 'Falha ao publicar o cliente de testes.' }
    # Only the isolated published copy is configured for writing.
    '{"ApiBaseUrl":"http://127.0.0.1:5100/","ReadOnlyMode":false}' | Set-Content -Encoding UTF8 "$state/client/wwwroot/appsettings.json"
    $apiProcess = Start-Process -FilePath $sdk -ArgumentList "`"$state/api/ADSync.Api.dll`" --urls http://127.0.0.1:5100 --contentRoot `"$root/ADSync.Api`"" -WorkingDirectory $root -WindowStyle Hidden -PassThru -RedirectStandardOutput "$state/api.log" -RedirectStandardError "$state/api-error.log"
    $clientProcess = Start-Process -FilePath (Get-Command node.exe).Source -ArgumentList 'tools/serve-cadastro-tests.cjs' -WorkingDirectory $root -WindowStyle Hidden -PassThru -RedirectStandardOutput "$state/client.log" -RedirectStandardError "$state/client-error.log"
    @{ Api = $apiProcess.Id; Client = $clientProcess.Id } | ConvertTo-Json | Set-Content "$state/processes.json"
    $up = $false
    for ($attempt = 0; $attempt -lt 40; $attempt++) {
        try { $runtime = Invoke-RestMethod 'http://127.0.0.1:5100/api/runtime'; if (!$runtime.readOnly) { $up = $true; break } } catch { }
        Start-Sleep -Milliseconds 500
    }
    if (!$up) { throw 'A API de testes não iniciou. Consulte .local-demo/cadastros/api-error.log.' }
    Write-Host 'Ambiente de testes: http://127.0.0.1:5126'
    Write-Host 'Credenciais locais: .local-demo/cadastros/credentials.json (não compartilhar).'
    Write-Host 'Supabase e aplicação nas portas 5099/5125 preservados.'
} finally {
    foreach ($key in $previous.Keys) { [Environment]::SetEnvironmentVariable($key, $previous[$key], 'Process') }
}
