<#
.SYNOPSIS
    Automated launcher for GPU-enabled Valheim 1.0 test harness integrated with VPM.
.DESCRIPTION
    Integrates with Valheim Profile Manager (VPM) to switch profiles via zero-copy
    NTFS directory junctions, verifies profile state, clears stale BepInEx logs,
    launches Valheim via Steam AppLaunch, monitors process initialization, and
    asserts clean boot without errors or patch failures.
.EXAMPLE
    .\harness\Start-ValheimHarness.ps1 -Profile isolated-sealcompanion -ClearLogs -AssertClean
#>
[CmdletBinding()]
param (
    [Parameter(Position = 0)]
    [string]$Profile,

    [string]$SteamPath = "C:\Program Files (x86)\Steam\steam.exe",
    [int]$AppId = 892970,
    [int]$TimeoutSeconds = 30,
    [switch]$ClearLogs,
    [switch]$AssertClean,
    [switch]$SkipVerify,
    [switch]$Force
)

$scriptDir = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
if ([string]::IsNullOrWhiteSpace($scriptDir)) { $scriptDir = (Get-Location).Path }

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "   DeepNorthTesting :: Valheim 1.0 Autonomous Launcher    " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. Profile Switch via Valheim Profile Manager (VPM)
if (-not [string]::IsNullOrWhiteSpace($Profile)) {
    Write-Host "`n[+] Invoking Valheim Profile Manager for: $Profile" -ForegroundColor Cyan
    $switchScript = Join-Path $scriptDir "..\tools\switch-profile.ps1"
    if (Test-Path $switchScript) {
        $forceArg = if ($Force) { "-Force" } else { "" }
        powershell -ExecutionPolicy Bypass -File $switchScript -Profile $Profile $forceArg
        if ($LASTEXITCODE -ne 0) {
            Write-Error "[FAIL] Profile switch failed. Aborting launch."
            exit 1
        }
    } else {
        Write-Warning "Profile switch script not found at: $switchScript"
    }
}

# 2. Profile State Integrity Verification
if (-not $SkipVerify) {
    $verifyScript = Join-Path $scriptDir "..\tools\Verify-ProfileState.ps1"
    if (Test-Path $verifyScript) {
        Write-Host "`n[+] Running Profile State Integrity Audit..." -ForegroundColor Gray
        powershell -ExecutionPolicy Bypass -File $verifyScript
        if ($LASTEXITCODE -ne 0) {
            Write-Error "[FAIL] Profile verification failed. Aborting launch."
            exit 1
        }
    }
}

# 3. Steam Process Verification
if (-not (Get-Process steam -ErrorAction SilentlyContinue)) {
    Write-Host "`n[!] Steam client is not running. Launching Steam..." -ForegroundColor Yellow
    Start-Process $SteamPath
    Start-Sleep -Seconds 5
}

# 4. Clear Stale Logs if requested
$logPath = "C:\Program Files (x86)\Steam\steamapps\common\Valheim\BepInEx\LogOutput.log"
if ($ClearLogs) {
    if (Test-Path $logPath) {
        Remove-Item $logPath -Force -ErrorAction SilentlyContinue
        Write-Host "`n[+] Cleared stale BepInEx LogOutput.log" -ForegroundColor Gray
    }
}

# 5. Dispatch Steam AppLaunch
Write-Host "`n[+] Dispatching Steam launch: AppId $AppId (-console)..." -ForegroundColor Green
Start-Process -FilePath $SteamPath -ArgumentList @("-applaunch", "$AppId", "-console")

# 6. Monitor Valheim Process Spawning
$deadline = (Get-Date).AddSeconds($TimeoutSeconds)
$valheimProcess = $null

do {
    Start-Sleep -Milliseconds 500
    $valheimProcess = Get-Process valheim -ErrorAction SilentlyContinue | Select-Object -First 1
} until ($valheimProcess -or (Get-Date) -ge $deadline)

if ($valheimProcess) {
    Write-Host "[OK] Valheim running with PID: $($valheimProcess.Id)" -ForegroundColor Green
    Write-Host "[+] Awaiting initialization and BepInEx chainloader..." -ForegroundColor Gray
    Start-Sleep -Seconds 8
    
    $valheimProcess.Refresh()
    Write-Host "[+] Process Status: Responding=$($valheimProcess.Responding), WorkingSet=$([math]::Round($valheimProcess.WorkingSet64 / 1MB, 1)) MB" -ForegroundColor Cyan

    # 7. Clean Boot Assertion
    if ($AssertClean) {
        Write-Host "`n[+] Running Clean Boot Assertion..." -ForegroundColor Gray
        $assertScript = Join-Path $scriptDir "Assert-CleanBoot.ps1"
        if (Test-Path $assertScript) {
            Start-Sleep -Seconds 2
            powershell -ExecutionPolicy Bypass -File $assertScript -LogPath $logPath
            if ($LASTEXITCODE -ne 0) {
                Write-Error "[FAIL] Boot assertions detected errors or exceptions."
                exit 1
            }
        } else {
            Write-Warning "Assert-CleanBoot script not found at: $assertScript"
        }
    }

    Write-Host "`n==========================================================" -ForegroundColor Green
    Write-Host " [READY] Valheim 1.0 is live and ready for testing!       " -ForegroundColor Green
    Write-Host "==========================================================" -ForegroundColor Green
} else {
    Write-Error "[FAIL] Valheim did not launch within $TimeoutSeconds seconds."
    exit 1
}
