# Construit le paquet QuickOTP (lanceur + app) et optionnellement publie une release GitHub.
param(
    [string]$Version = "1.0.0",
    [string]$GitHubRepo = "bipowww/QuickOTP",
    [switch]$PublishGitHub
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$out = Join-Path $root "dist\QuickOTP-$Version"
$zip = Join-Path $root "dist\QuickOTP-$Version-win-x64.zip"
$feedOut = Join-Path $root "dist\update.json"

Remove-Item $out -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path "$out\app" | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $root "dist") | Out-Null

Write-Host "== Build launcher ==" -ForegroundColor Cyan
dotnet publish "src\QuickOTP.Launcher\QuickOTP.Launcher.csproj" -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:Version=$Version -o "$out"

# Rename launcher stays QuickOTP.exe (AssemblyName)

Write-Host "== Package app binaries ==" -ForegroundColor Cyan
$appSrc = Join-Path $env:LOCALAPPDATA "QuickOTP"
if (-not (Test-Path (Join-Path $appSrc "QuickOTP.exe"))) {
    throw "App introuvable dans $appSrc - lance/installe QuickOTP une fois avant de publier."
}

# Binaires seulement — jamais tokens / client_secret / accounts
Copy-Item (Join-Path $appSrc "QuickOTP.exe") (Join-Path $out "app\QuickOTPApp.exe") -Force
Get-ChildItem $appSrc -File | Where-Object {
    $_.Extension -in ".dll", ".json", ".pdb" -and
    $_.Name -notmatch "^(client_secret|accounts|rules|msal)" -and
    $_.Name -ne "app.log"
} | ForEach-Object {
    Copy-Item $_.FullName (Join-Path $out "app\$($_.Name)") -Force
}
if (Test-Path (Join-Path $appSrc "Assets")) {
    Copy-Item (Join-Path $appSrc "Assets") (Join-Path $out "app\Assets") -Recurse -Force
}

# Feed URL for this install
"https://github.com/$GitHubRepo/releases/latest/download/update.json" |
    Set-Content (Join-Path $out "update-feed.url") -Encoding ascii -NoNewline
$Version | Set-Content (Join-Path $out "version.txt") -Encoding ascii -NoNewline

@'
QuickOTP
========
1. Dezipper ce dossier ou lancer QuickOTP.exe
2. Prerequisite: .NET 8 Desktop Runtime (Windows x64)
   https://dotnet.microsoft.com/download/dotnet/8.0
3. Gmail: placez votre client_secret.json dans %LocalAppData%\QuickOTP\

Les mises a jour se telechargent automatiquement au demarrage
lorsqu une nouvelle version est publiee.
'@ | Set-Content (Join-Path $out "LISEZMOI.txt") -Encoding UTF8

if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path "$out\*" -DestinationPath $zip -Force

$sha = (Get-FileHash $zip -Algorithm SHA256).Hash
$downloadUrl = "https://github.com/$GitHubRepo/releases/download/v$Version/QuickOTP-$Version-win-x64.zip"
$manifest = [ordered]@{
    version = $Version
    url     = $downloadUrl
    sha256  = $sha
    notes   = "QuickOTP $Version"
}
$manifest | ConvertTo-Json | Set-Content $feedOut -Encoding utf8

Write-Host "ZIP: $zip" -ForegroundColor Green
Write-Host "SHA256: $sha"
Write-Host "Manifest: $feedOut"

if ($PublishGitHub) {
    if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
        throw "GitHub CLI (gh) requis pour -PublishGitHub. Installez: winget install GitHub.cli"
    }
    Write-Host "== GitHub release v$Version ==" -ForegroundColor Cyan
    gh release create "v$Version" $zip $feedOut `
        --repo $GitHubRepo `
        --title "QuickOTP $Version" `
        --notes "Mise a jour automatique disponible pour les installations existantes." `
        --latest
    Write-Host "Release: https://github.com/$GitHubRepo/releases/tag/v$Version" -ForegroundColor Green
}
