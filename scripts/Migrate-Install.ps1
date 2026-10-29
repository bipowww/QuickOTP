# Migre vers l'install lanceur et retire l'ancien exe du dossier data.
# Conserve tokens / client_secret / accounts / history dans %LocalAppData%\QuickOTP.
param(
    [string]$Version = "1.0.1"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

$package = Join-Path $root "dist\QuickOTP-$Version"
if (-not (Test-Path (Join-Path $package "QuickOTP.exe"))) {
    Write-Host "Build package $Version..." -ForegroundColor Cyan
    & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot "Publish-Update.ps1") -Version $Version
}

& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot "Install-Local.ps1") -Version $Version

$data = Join-Path $env:LOCALAPPDATA "QuickOTP"
$legacyExe = Join-Path $data "QuickOTP.exe"
$legacyDll = Join-Path $data "QuickOTP.dll"
$backup = Join-Path $data "_legacy_bin_backup"
if (Test-Path $legacyExe) {
    New-Item -ItemType Directory -Force -Path $backup | Out-Null
    Write-Host "Deplace les anciens binaires data -> $backup" -ForegroundColor Yellow
    foreach ($name in @("QuickOTP.exe","QuickOTP.dll","QuickOTP.Core.dll","QuickOTP.deps.json","QuickOTP.runtimeconfig.json","QuickOTP.pdb","QuickOTP.Core.pdb")) {
        $p = Join-Path $data $name
        if (Test-Path $p) { Move-Item $p (Join-Path $backup $name) -Force }
    }
}

Write-Host "OK. Lance QuickOTP depuis le menu Demarrer (Programs\QuickOTP)." -ForegroundColor Green
Write-Host "Donnees conservees: $data"
