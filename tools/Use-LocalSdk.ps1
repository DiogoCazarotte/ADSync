$root = Split-Path $PSScriptRoot -Parent
$localSdk = Join-Path $root '.local-demo/dotnet'
if (Test-Path (Join-Path $localSdk 'dotnet.exe')) {
    $env:DOTNET_ROOT = $localSdk
    if ($env:PATH.Split(';') -notcontains $localSdk) { $env:PATH = "$localSdk;$env:PATH" }
}
dotnet --version
