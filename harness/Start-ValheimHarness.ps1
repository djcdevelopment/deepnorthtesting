<#
.SYNOPSIS
    Automated launcher for GPU-enabled Valheim 1.0 test harness.
.DESCRIPTION
    Launches Valheim via Steam AppLaunch to connect directly to the authenticated
    Steam session without modal dialogs, bypasses startup cinematics (<8s boot),
    and tracks PID and process readiness.
#>
param (
    [string]$SteamPath = "C:\Program Files (x86)\Steam\steam.exe",
    [int]$AppId = 892970,
    [int]$TimeoutSeconds = 30,
    [switch]$ClearLogs
)

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "   DeepNorthTesting :: Valheim 1.0 Autonomous Launcher    " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

if (-not (Get-Process steam -ErrorAction SilentlyContinue)) {
    Write-Host "[!] Steam client is not running. Launching Steam..." -ForegroundColor Yellow
    Start-Process $SteamPath
    Start-Sleep -Seconds 5
}

if ($ClearLogs) {
    $logPath = "C:\Program Files (x86)\Steam\steamapps\common\Valheim\BepInEx\LogOutput.log"
    if (Test-Path $logPath) {
        Remove-Item $logPath -Force -ErrorAction SilentlyContinue
        Write-Host "[+] Cleared stale BepInEx LogOutput.log" -ForegroundColor Gray
    }
}

Write-Host "[+] Dispatching Steam launch: AppId $AppId (-console)..." -ForegroundColor Green
Start-Process -FilePath $SteamPath -ArgumentList @("-applaunch", "$AppId", "-console")

$deadline = (Get-Date).AddSeconds($TimeoutSeconds)
$valheimProcess = $null

do {
    Start-Sleep -Milliseconds 500
    $valheimProcess = Get-Process valheim -ErrorAction SilentlyContinue | Select-Object -First 1
} until ($valheimProcess -or (Get-Date) -ge $deadline)

if ($valheimProcess) {
    Write-Host "[✓] Valheim running with PID: $($valheimProcess.Id)" -ForegroundColor Green
    Write-Host "[+] Awaiting initialization and BepInEx chainloader..." -ForegroundColor Gray
    Start-Sleep -Seconds 8
    
    $valheimProcess.Refresh()
    Write-Host "[+] Process Status: Responding=$($valheimProcess.Responding), WorkingSet=$([math]::Round($valheimProcess.WorkingSet64 / 1MB, 1)) MB" -ForegroundColor Cyan
} else {
    Write-Error "[✗] Valheim did not launch within $TimeoutSeconds seconds."
    exit 1
}
