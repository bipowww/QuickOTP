# Construit le paquet QuickOTP (lanceur + app) et optionnellement publie une release GitHub.
# Priorite des sources app:
#   1) -AppSource explicite
#   2) publish\app (sortie d'un dotnet publish app)
#   3) %LocalAppData%\Programs\QuickOTP\app (install lanceur)
# Jamais le dossier data user (%LocalAppData%\QuickOTP) pour eviter tokens/secrets.
param(
    [Parameter(Mandatory = $true)]
    [string]$Version,
    [string]$GitHubRepo = "bipowww/QuickOTP",
    [string]$AppSource = "",
    [switch]$PublishGitHub
)

$ErrorActionPreference = "Stop"
$utf8NoBom = New-Object System.Text.UTF8Encoding $false
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

function Resolve-AppSource {
    param([string]$Explicit)
    if ($Explicit -and (Test-Path (Join-Path $Explicit "QuickOTPApp.exe") -or Test-Path (Join-Path $Explicit "QuickOTP.exe"))) {
        return (Resolve-Path $Explicit).Path
    }
    $candidates = @(
        (Join-Path $root "publish\app"),
        (Join-Path $root "artifacts\app"),
        (Join-Path $env:LOCALAPPDATA "Programs\QuickOTP\app")
    )
    foreach ($c in $candidates) {
        $exeA = Join-Path $c "QuickOTPApp.exe"
        $exeB = Join-Path $c "QuickOTP.exe"
        if ((Test-Path $exeA) -or (Test-Path $exeB)) { return $c }
    }
    return $null
}

$out = Join-Path $root "dist\QuickOTP-$Version"
$zip = Join-Path $root "dist\QuickOTP-$Version-win-x64.zip"
$feedOut = Join-Path $root "dist\update.json"

Remove-Item $out -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path "$out\app" | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $root "dist") | Out-Null

# Rebuild app (UI + Core) into artifacts when sources are present
$uiProj = Join-Path $root "src\QuickOTP\QuickOTP.csproj"
$artifactsApp = Join-Path $root "artifacts\app"
if (Test-Path $uiProj) {
    Write-Host "== Build QuickOTP app (WPF + Core) ==" -ForegroundColor Cyan
    New-Item -ItemType Directory -Force -Path $artifactsApp | Out-Null
    $appOut = Join-Path $root "artifacts\app-build"
    Remove-Item $appOut -Recurse -Force -ErrorAction SilentlyContinue
    dotnet publish $uiProj -c Release -r win-x64 --self-contained false -p:DebugType=none -p:Version=$Version -o $appOut
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish app failed" }
    Copy-Item (Join-Path $appOut "QuickOTP.exe") (Join-Path $artifactsApp "QuickOTPApp.exe") -Force
    Get-ChildItem $appOut -File | Where-Object {
        $_.Extension -in ".dll", ".json" -and $_.Name -notlike "*.pdb"
    } | ForEach-Object {
        Copy-Item $_.FullName (Join-Path $artifactsApp $_.Name) -Force
    }
    if (Test-Path (Join-Path $appOut "Assets")) {
        Copy-Item (Join-Path $appOut "Assets") (Join-Path $artifactsApp "Assets") -Recurse -Force
    }
    Remove-Item (Join-Path $artifactsApp "*.pdb") -Force -ErrorAction SilentlyContinue
}

Write-Host "== Build launcher ==" -ForegroundColor Cyan
dotnet publish "src\QuickOTP.Launcher\QuickOTP.Launcher.csproj" -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none `
  -p:Version=$Version -o "$out"
if ($LASTEXITCODE -ne 0) { throw "dotnet publish launcher failed" }
Remove-Item (Join-Path $out "*.pdb") -Force -ErrorAction SilentlyContinue

Write-Host "== Package app binaries ==" -ForegroundColor Cyan
$appSrc = Resolve-AppSource -Explicit $AppSource
if (-not $appSrc) {
    throw @"
App introuvable. Fournissez -AppSource ou placez une build dans:
  publish\app  |  artifacts\app  |  %LocalAppData%\Programs\QuickOTP\app
"@
}

Write-Host "App source: $appSrc"
$mainExe = Join-Path $appSrc "QuickOTPApp.exe"
if (-not (Test-Path $mainExe)) { $mainExe = Join-Path $appSrc "QuickOTP.exe" }
Copy-Item $mainExe (Join-Path $out "app\QuickOTPApp.exe") -Force

Get-ChildItem $appSrc -File | Where-Object {
    $_.Extension -in ".dll", ".json" -and
    $_.Name -notmatch "^(client_secret|accounts|rules|msal|settings|history)" -and
    $_.Name -ne "app.log"
} | ForEach-Object {
    Copy-Item $_.FullName (Join-Path $out "app\$($_.Name)") -Force
}
if (Test-Path (Join-Path $appSrc "Assets")) {
    Copy-Item (Join-Path $appSrc "Assets") (Join-Path $out "app\Assets") -Recurse -Force
}

# Nettoyage securite: jamais de PDB / secrets dans le zip
Remove-Item (Join-Path $out "app\*.pdb") -Force -ErrorAction SilentlyContinue
Get-ChildItem (Join-Path $out "app") -Recurse -File -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -match "^(client_secret|accounts|rules|msal|tokens|history)" } |
    Remove-Item -Force -ErrorAction SilentlyContinue

$feedUrl = "https://github.com/$GitHubRepo/releases/latest/download/update.json"
[System.IO.File]::WriteAllText((Join-Path $out "update-feed.url"), $feedUrl, $utf8NoBom)
[System.IO.File]::WriteAllText((Join-Path $out "version.txt"), $Version, $utf8NoBom)

$readme = @"
QuickOTP
========
1. Dezipper ce dossier puis lancer QuickOTP.exe
2. Prerequisite: .NET 8 Desktop Runtime (Windows x64)
   https://dotnet.microsoft.com/download/dotnet/8.0
3. Gmail: placez votre client_secret.json dans %LocalAppData%\QuickOTP\

Les mises a jour s'installent automatiquement au demarrage
lorsqu'une nouvelle version est publiee sur GitHub Releases.
"@
[System.IO.File]::WriteAllText((Join-Path $out "LISEZMOI.txt"), $readme, $utf8NoBom)

if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path "$out\*" -DestinationPath $zip -Force

$sha = (Get-FileHash $zip -Algorithm SHA256).Hash
$downloadUrl = "https://github.com/$GitHubRepo/releases/download/v$Version/QuickOTP-$Version-win-x64.zip"
$manifestObj = [ordered]@{
    version = $Version
    url     = $downloadUrl
    sha256  = $sha
    notes   = "QuickOTP $Version"
}
$manifestJson = ($manifestObj | ConvertTo-Json)
[System.IO.File]::WriteAllText($feedOut, $manifestJson, $utf8NoBom)

Write-Host "ZIP: $zip" -ForegroundColor Green
Write-Host "SHA256: $sha"
Write-Host "Manifest: $feedOut"

if ($PublishGitHub) {
    if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
        throw "GitHub CLI (gh) requis pour -PublishGitHub. Installez: winget install GitHub.cli"
    }
    Write-Host "== GitHub release v$Version ==" -ForegroundColor Cyan
    $prevEap = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    gh release view "v$Version" --repo $GitHubRepo 2>$null | Out-Null
    $exists = ($LASTEXITCODE -eq 0)
    $ErrorActionPreference = $prevEap
    if ($exists) {
        gh release upload "v$Version" $zip $feedOut --repo $GitHubRepo --clobber
        if ($LASTEXITCODE -ne 0) { throw "gh release upload failed" }
    } else {
        gh release create "v$Version" $zip $feedOut `
            --repo $GitHubRepo `
            --title "QuickOTP $Version" `
            --notes "Mise a jour automatique disponible pour les installations existantes." `
            --latest
        if ($LASTEXITCODE -ne 0) { throw "gh release create failed" }
    }
    Write-Host "Release: https://github.com/$GitHubRepo/releases/tag/v$Version" -ForegroundColor Green
    Write-Host "Lien amis: https://github.com/$GitHubRepo/releases/latest" -ForegroundColor Green
}
