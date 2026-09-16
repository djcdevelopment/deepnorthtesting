# Zero-Copy Manifest Manager & Profile Switcher for Valheim BepInEx
# Valheim Profile Engine :: Autonomous Test Harness

[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [string]$Profile,

    [switch]$Status,
    [switch]$List,
    [switch]$Fleet,
    [switch]$Force,
    [string]$ManifestPath
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($ManifestPath)) {
    $scriptDir = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
    if ([string]::IsNullOrWhiteSpace($scriptDir)) { $scriptDir = (Get-Location).Path }
    $ManifestPath = Join-Path $scriptDir "..\manifests\profiles.json"
}

# 1. Load Manifest
if (-not (Test-Path $ManifestPath)) {
    Write-Error "Manifest not found at: $ManifestPath"
    return
}

$manifest = Get-Content $ManifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
$machineKey = if ($manifest.default_machine -and $manifest.machines.$($manifest.default_machine)) {
    $manifest.default_machine
} elseif ($manifest.machines.local) {
    "local"
} elseif ($manifest.machines.default) {
    "default"
} else {
    ($manifest.machines.PSObject.Properties | Select-Object -First 1).Name
}
$machineConfig = $manifest.machines.$machineKey
$gameDir = $machineConfig.game_dir
$bepDir = $machineConfig.bepinex_dir
$pluginsDir = Join-Path $bepDir "plugins"
$profilesDir = Join-Path $bepDir "profiles"

# Helper: Test if path is an NTFS reparse point (junction / symlink)
function Test-ReparsePoint([string]$Path) {
    if (-not (Test-Path $Path)) { return $false }
    $item = Get-Item $Path -Force
    return [bool]($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint)
}

# Helper: Safely drop junction reparse point (preserves target contents 100%)
function Remove-JunctionSafe([string]$Path) {
    if (Test-ReparsePoint $Path) {
        $null = cmd /c "rmdir `"$Path`""
        if (Test-Path $Path) {
            [System.IO.Directory]::Delete($Path)
        }
    }
}

# Helper: Create directory junction
function New-Junction([string]$LinkPath, [string]$TargetPath) {
    if (Test-ReparsePoint $LinkPath) {
        Remove-JunctionSafe $LinkPath
    } elseif (Test-Path $LinkPath) {
        Remove-Item $LinkPath -Recurse -Force
    }
    $targetWinPath = $TargetPath.Replace('/', '\')
    $linkWinPath = $LinkPath.Replace('/', '\')
    $output = cmd /c "mklink /J `"$linkWinPath`" `"$targetWinPath`""
    return $output
}

# Helper: Create NTFS hardlink for files
function New-HardlinkSafe([string]$LinkPath, [string]$SourcePath) {
    if (-not (Test-Path $SourcePath)) {
        Write-Warning "Source file does not exist for hardlink: $SourcePath"
        return
    }
    if (Test-Path $LinkPath) {
        Remove-Item $LinkPath -Force
    }
    $sourceWinPath = $SourcePath.Replace('/', '\')
    $linkWinPath = $LinkPath.Replace('/', '\')
    $null = cmd /c "mklink /H `"$linkWinPath`" `"$sourceWinPath`""
}

# Helper: Resolve profile by name or alias
function Resolve-Profile([string]$Query) {
    if ([string]::IsNullOrWhiteSpace($Query)) { return $null }
    $props = $manifest.profiles.PSObject.Properties
    foreach ($p in $props) {
        if ($p.Name -ieq $Query) {
            return @{ Name = $p.Name; Data = $p.Value }
        }
        if ($p.Value.alias -and ($p.Value.alias -contains $Query.ToLower())) {
            return @{ Name = $p.Name; Data = $p.Value }
        }
    }
    return $null
}

# Helper: Display current status
function Show-Status {
    Write-Host "`n======================================================" -ForegroundColor Cyan
    Write-Host "  Valheim Profile Engine :: BepInEx Profile State ($machineKey)" -ForegroundColor Cyan
    Write-Host "======================================================" -ForegroundColor Cyan

    $isJunction = Test-ReparsePoint $pluginsDir
    $activeName = $manifest.active_profile

    Write-Host " Plugins Path   : " -NoNewline; Write-Host $pluginsDir -ForegroundColor Yellow
    Write-Host " Is Zero-Copy   : " -NoNewline
    if ($isJunction) {
        Write-Host "YES (NTFS Junction)" -ForegroundColor Green
        $target = (Get-Item $pluginsDir -Force).Target
        Write-Host " Junction Target: " -NoNewline; Write-Host $target -ForegroundColor Green
    } else {
        Write-Host "NO (Standard directory - files copied physically)" -ForegroundColor Red
    }
    Write-Host " Active Profile : " -NoNewline; Write-Host $activeName -ForegroundColor Magenta

    if (Test-Path $pluginsDir) {
        $files = Get-ChildItem $pluginsDir -Recurse -Filter "*.dll" -ErrorAction SilentlyContinue
        Write-Host " Active DLLs    : " -NoNewline; Write-Host "$($files.Count) assemblies loaded" -ForegroundColor White
        Write-Host "`nActive Plugins Breakdown:" -ForegroundColor DarkCyan
        Get-ChildItem $pluginsDir | Select-Object Name, Length, LastWriteTime | Format-Table -AutoSize
    }
}

# 2. Handle -List
if ($List) {
    Write-Host "`nAvailable Profiles in $ManifestPath :" -ForegroundColor Cyan
    $props = $manifest.profiles.PSObject.Properties
    foreach ($p in $props) {
        $aliases = if ($p.Value.alias) { " [alias: $($p.Value.alias -join ', ')]" } else { "" }
        Write-Host "  * " -NoNewline; Write-Host $p.Name -ForegroundColor Yellow -NoNewline
        Write-Host $aliases -ForegroundColor DarkGray
        Write-Host "    $($p.Value.description)" -ForegroundColor Gray
    }
    return
}

# 3. Handle -Fleet
if ($Fleet) {
    python -c "import sys; sys.path.insert(0, r'c:\work\isolate\network\mcp'); from comfy_gateway.toolsurface.fleet import fleet_mod_matrix; print(fleet_mod_matrix())"
    return
}

# 4. Handle -Status or empty Profile param
if ($Status -or [string]::IsNullOrWhiteSpace($Profile)) {
    Show-Status
    return
}

# 4. Resolve Target Profile
$resolved = Resolve-Profile $Profile
if (-not $resolved) {
    Write-Error "Unknown profile: '$Profile'. Run with -List to see available profiles."
    return
}

$targetProfileName = $resolved.Name
$targetProfileData = $resolved.Data

# Safety: Check if Valheim is running
$valheimProc = Get-Process valheim* -ErrorAction SilentlyContinue
if ($valheimProc) {
    Write-Warning "Valheim process ($($valheimProc.Id)) is currently running!"
    Write-Warning "Switching plugins while the engine is running may cause read locks."
    if (-not $Force) {
        Write-Host "Aborting switch. Close Valheim first or use -Force to proceed anyway." -ForegroundColor Red
        return
    }
}

$sw = [System.Diagnostics.Stopwatch]::StartNew()

# 5. Prepare Target Profile on Disk
$profileTargetDir = $null

if ($targetProfileData.type -eq "directory") {
    $profileTargetDir = $targetProfileData.path
    if (-not (Test-Path $profileTargetDir)) {
        New-Item -ItemType Directory -Path $profileTargetDir -Force | Out-Null
    }
} elseif ($targetProfileData.type -eq "synthetic") {
    $profileTargetDir = $targetProfileData.target_dir
    if (-not (Test-Path $profileTargetDir)) {
        New-Item -ItemType Directory -Path $profileTargetDir -Force | Out-Null
    }
    # Resolve synthetic entries
    foreach ($entry in $targetProfileData.entries) {
        $destPath = Join-Path $profileTargetDir $entry.name
        if ($entry.link_type -eq "junction") {
            if (-not (Test-Path $entry.source)) {
                Write-Warning "Compiler source directory not found: $($entry.source)"
                continue
            }
            New-Junction $destPath $entry.source | Out-Null
        } elseif ($entry.link_type -eq "hardlink") {
            if (-not (Test-Path $entry.source)) {
                Write-Warning "File source not found: $($entry.source)"
                continue
            }
            New-HardlinkSafe $destPath $entry.source
        }
    }
}

# 6. Safety Check on existing pluginsDir
if (Test-Path $pluginsDir) {
    if (-not (Test-ReparsePoint $pluginsDir)) {
        # It is a physical directory! Check if it contains files not yet backed up
        $currentCount = (Get-ChildItem $pluginsDir -Recurse -File).Count
        if ($currentCount -gt 0) {
            $backupDir = Join-Path $profilesDir "backup-before-junction"
            Write-Host "[!] Non-junction plugins folder detected with $currentCount files." -ForegroundColor Yellow
            Write-Host "[+] Backing up to $backupDir before converting to zero-copy junction..." -ForegroundColor Cyan
            if (-not (Test-Path $backupDir)) {
                New-Item -ItemType Directory -Path $backupDir -Force | Out-Null
            }
            Copy-Item "$pluginsDir\*" -Destination $backupDir -Recurse -Force
        }
        # Safely remove physical folder
        Remove-Item $pluginsDir -Recurse -Force
    } else {
        # Safely unlink junction
        Remove-JunctionSafe $pluginsDir
    }
}

# 7. Create Zero-Copy Junction Pointer
New-Junction $pluginsDir $profileTargetDir | Out-Null

$sw.Stop()

# 8. Update Active Profile in Manifest
$manifest.active_profile = $targetProfileName
$manifestJson = $manifest | ConvertTo-Json -Depth 10
[System.IO.File]::WriteAllText((Resolve-Path $ManifestPath), $manifestJson, [System.Text.Encoding]::UTF8)

Write-Host "`n[OK] Switched to profile: " -NoNewline -ForegroundColor Green
Write-Host $targetProfileName -ForegroundColor Yellow -NoNewline
Write-Host " in $($sw.ElapsedMilliseconds) ms (Zero File Copies!)" -ForegroundColor Green

Show-Status
