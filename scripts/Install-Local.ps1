# Installe le paquet QuickOTP (lanceur + auto-update) pour cet utilisateur Windows.
param(
    [string]$PackageDir = "",
    [string]$Version = "1.0.0"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

if (-not $PackageDir) {
    $PackageDir = Join-Path $root "dist\QuickOTP-$Version"
}
if (-not (Test-Path (Join-Path $PackageDir "QuickOTP.exe"))) {
    throw "Paquet introuvable: $PackageDir - lancez d'abord Publish-Update.ps1"
}

$install = Join-Path $env:LOCALAPPDATA "Programs\QuickOTP"
New-Item -ItemType Directory -Force -Path $install | Out-Null

Write-Host "Installation vers $install" -ForegroundColor Cyan
Copy-Item "$PackageDir\*" $install -Recurse -Force

$startMenu = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs"
New-Item -ItemType Directory -Force -Path $startMenu | Out-Null
$shortcutPath = Join-Path $startMenu "QuickOTP.lnk"

$wsh = New-Object -ComObject WScript.Shell
$sc = $wsh.CreateShortcut($shortcutPath)
$sc.TargetPath = Join-Path $install "QuickOTP.exe"
$sc.WorkingDirectory = $install
$sc.Description = "QuickOTP"
$sc.Save()

Write-Host "OK - lancez QuickOTP depuis le menu Demarrer." -ForegroundColor Green
Write-Host "Donnees Gmail restent dans: $env:LOCALAPPDATA\QuickOTP"
