param(
    [string]$SecretsPath,
    [string]$OutputPath
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
if (!$OutputPath) { $OutputPath = Join-Path $projectRoot '.env.docker' }
if (Test-Path -LiteralPath $OutputPath) {
    throw 'O arquivo de destino já existe. Ele foi preservado; edite-o privadamente se precisar alterar a configuração.'
}
if (!$SecretsPath) {
    [xml]$project = Get-Content -Raw -Encoding UTF8 -LiteralPath (Join-Path $projectRoot 'ADSync.Api/ADSync.Api.csproj')
    $secretsId = [string]$project.Project.PropertyGroup.UserSecretsId
    $SecretsPath = Join-Path $env:APPDATA "Microsoft/UserSecrets/$secretsId/secrets.json"
}
if (!(Test-Path -LiteralPath $SecretsPath)) {
    throw 'User Secrets não encontrados. Copie .env.docker.example para .env.docker e preencha conexão e chave JWT privadamente.'
}
$secrets = Get-Content -Raw -Encoding UTF8 -LiteralPath $SecretsPath | ConvertFrom-Json
$connection = [string]$secrets.'ConnectionStrings:AdSync'
if (!$connection) { $connection = [string]$secrets.ConnectionStrings.AdSync }
$jwt = [string]$secrets.'Jwt:Key'
if (!$jwt) { $jwt = [string]$secrets.Jwt.Key }
if ([string]::IsNullOrWhiteSpace($connection)) { throw 'A conexão AdSync não foi encontrada nos User Secrets.' }
if ([string]::IsNullOrWhiteSpace($jwt) -or $jwt.Length -lt 32) { throw 'A chave JWT existente deve ter ao menos 32 caracteres.' }
if ($connection.Contains("`n") -or $connection.Contains("`r") -or $jwt.Contains("`n") -or $jwt.Contains("`r")) {
    throw 'Conexão e chave devem estar em uma única linha.'
}
# Compose usa format: raw para preservar $, #, aspas e barras dos valores existentes.
# O destino é ignorado pelo Git e não faz parte do contexto da imagem.
$lines = @(
    "ConnectionStrings__AdSync=$connection",
    "Jwt__Key=$jwt",
    'Safety__ReadOnly=false',
    'Offline__Enabled=true',
    'Preventivas__GeracaoAutomaticaHabilitada=true'
)
[IO.File]::WriteAllText([IO.Path]::GetFullPath($OutputPath), ($lines -join "`n") + "`n", (New-Object Text.UTF8Encoding($false)))
Write-Host 'Configuração Docker criada com a conexão e a chave JWT existentes. Nenhum valor foi exibido; nenhum banco foi acessado.'
