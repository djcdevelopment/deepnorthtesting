param(
    [ValidateSet("clean", "full")]
    [string]$Profile = "clean"
)

$ErrorActionPreference = "Stop"

$gameDir = "C:\Program Files (x86)\Steam\steamapps\common\Valheim"
$bepDir = Join-Path $gameDir "BepInEx"
$pluginsDir = Join-Path $bepDir "plugins"
$profilesDir = Join-Path $bepDir "profiles"
$fullProfile = Join-Path $profilesDir "full-comfymods"
$cleanProfile = Join-Path $profilesDir "clean-recording"

# Ensure directories exist
if (-not (Test-Path $fullProfile)) {
    New-Item -ItemType Directory -Force -Path $fullProfile | Out-Null
}
if (-not (Test-Path $cleanProfile)) {
    New-Item -ItemType Directory -Force -Path $cleanProfile | Out-Null
}

# 1. First-time backup of existing plugins into full-comfymods if empty
$currentFullCount = (Get-ChildItem $fullProfile).Count
if ($currentFullCount -eq 0) {
    Write-Host "[+] Backing up current plugins to full-comfymods profile..." -ForegroundColor Cyan
    Get-ChildItem -Path $pluginsDir | Copy-Item -Destination $fullProfile -Recurse -Force
}

# 2. Ensure clean profile has ComfyCameraProof.dll and Unfaded.dll
$cameraSrc = "C:\work\baseline\tools\camera-kit\mods\ComfyCameraProof.dll"
$unfadedSrc = "C:\work\deepnorthtesting\plugins\Unfaded\bin\Debug\Unfaded.dll"

if (Test-Path $cameraSrc) {
    Copy-Item $cameraSrc (Join-Path $cleanProfile "ComfyCameraProof.dll") -Force
}
if (Test-Path $unfadedSrc) {
    Copy-Item $unfadedSrc (Join-Path $cleanProfile "Unfaded.dll") -Force
}

# 3. Switch active plugins
if ($Profile -eq "clean") {
    Write-Host "[+] Switching to CLEAN profile (ComfyCameraProof + Unfaded only)..." -ForegroundColor Green
    # Clear plugins folder
    Get-ChildItem -Path $pluginsDir | Remove-Item -Recurse -Force
    # Copy clean profile files
    Get-ChildItem -Path $cleanProfile | Copy-Item -Destination $pluginsDir -Recurse -Force
}
elseif ($Profile -eq "full") {
    Write-Host "[+] Restoring FULL profile (all 65 Comfy mods)..." -ForegroundColor Yellow
    # Clear plugins folder
    Get-ChildItem -Path $pluginsDir | Remove-Item -Recurse -Force
    # Copy full profile files
    Get-ChildItem -Path $fullProfile | Copy-Item -Destination $pluginsDir -Recurse -Force
}

Write-Host "`n[+] Current Active Plugins in $pluginsDir :" -ForegroundColor Cyan
Get-ChildItem $pluginsDir | Select-Object Name, Length, LastWriteTime | Format-Table -AutoSize
