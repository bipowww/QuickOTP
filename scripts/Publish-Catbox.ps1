# Alternative de partage si GitHub Releases n'est pas encore configure:
# upload le zip + un update.json pointant vers l'URL Catbox du zip.
# Note: l'URL du feed change a chaque upload — pour les maj suivantes,
# preferer Publish-Update.ps1 -PublishGitHub (URL feed stable).
param(
    [string]$Version = "1.0.0"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$zip = Join-Path $root "dist\QuickOTP-$Version-win-x64.zip"
if (-not (Test-Path $zip)) {
    throw "ZIP manquant: $zip - lancez Publish-Update.ps1 d'abord"
}

Write-Host "Upload Catbox du zip..." -ForegroundColor Cyan
$resp = curl.exe -s -F "reqtype=fileupload" -F "fileToUpload=@$zip" "https://catbox.moe/user/api.php"
if (-not $resp -or $resp -notmatch "^https?://") {
    throw "Echec upload Catbox: $resp"
}
$zipUrl = $resp.Trim()
$sha = (Get-FileHash $zip -Algorithm SHA256).Hash

$tmpManifest = Join-Path $env:TEMP "quickotp-update-$Version.json"
@{
    version = $Version
    url     = $zipUrl
    sha256  = $sha
    notes   = "QuickOTP $Version"
} | ConvertTo-Json | Set-Content $tmpManifest -Encoding utf8

Write-Host "Upload Catbox du manifeste..." -ForegroundColor Cyan
$feedResp = curl.exe -s -F "reqtype=fileupload" -F "fileToUpload=@$tmpManifest" "https://catbox.moe/user/api.php"
if (-not $feedResp -or $feedResp -notmatch "^https?://") {
    throw "Echec upload manifeste: $feedResp"
}
$feedUrl = $feedResp.Trim()

# Met a jour le feed local du paquet / install pour pointer vers ce manifeste
$targets = @(
    (Join-Path $root "dist\QuickOTP-$Version\update-feed.url"),
    (Join-Path $env:LOCALAPPDATA "Programs\QuickOTP\update-feed.url")
)
foreach ($t in $targets) {
    if (Test-Path (Split-Path $t -Parent)) {
        Set-Content $t $feedUrl -Encoding ascii -NoNewline
    }
}

Write-Host ""
Write-Host "ZIP amis:  $zipUrl" -ForegroundColor Green
Write-Host "Feed maj:  $feedUrl" -ForegroundColor Green
Write-Host "SHA256:    $sha"
Write-Host ""
Write-Host "IMPORTANT: pour des maj stables (URL feed fixe), passe a GitHub Releases:"
Write-Host "  gh auth login"
Write-Host "  .\scripts\Publish-Update.ps1 -Version x.y.z -PublishGitHub"
