<#
.SYNOPSIS
    Automated log assertion script for Valheim 1.0 test harness.
.DESCRIPTION
    Scrapes BepInEx/LogOutput.log and asserts zero errors, exceptions, or patch failures.
#>
param (
    [string]$LogPath = "C:\Program Files (x86)\Steam\steamapps\common\Valheim\BepInEx\LogOutput.log"
)

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "   DeepNorthTesting :: Valheim 1.0 Clean Boot Assertion   " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

if (-not (Test-Path $LogPath)) {
    Write-Error "[✗] Log file not found at: $LogPath"
    exit 1
}

Write-Host "[+] Parsing log: $LogPath" -ForegroundColor Gray

$errorMatches = Select-String -Path $LogPath -Pattern '\[Error\s*:'
$exceptionMatches = Select-String -Path $LogPath -Pattern '(?i)(NullReferenceException|InvalidOperationException|MissingMethodException|AmbiguousMatchException)'
$harmonyFailures = Select-String -Path $LogPath -Pattern 'Failed to patch'

$totalErrors = ($errorMatches | Measure-Object).Count
$totalExceptions = ($exceptionMatches | Measure-Object).Count
$totalFailures = ($harmonyFailures | Measure-Object).Count

Write-Host "`n--- Assertion Results ---"
if ($totalErrors -eq 0 -and $totalExceptions -eq 0 -and $totalFailures -eq 0) {
    Write-Host "[PASS] 0 Errors, 0 Exceptions, 0 Patch Failures detected." -ForegroundColor Green
    Write-Host "[PASS] All active BepInEx plugins loaded and patched cleanly into Valheim 1.0." -ForegroundColor Green
    exit 0
} else {
    Write-Host "[FAIL] Detected issues during boot:" -ForegroundColor Red
    if ($totalErrors -gt 0) {
        Write-Host "  - Errors: $totalErrors" -ForegroundColor Red
        $errorMatches | ForEach-Object { Write-Host "    $($_.Line)" -ForegroundColor DarkRed }
    }
    if ($totalExceptions -gt 0) {
        Write-Host "  - Exceptions: $totalExceptions" -ForegroundColor Red
        $exceptionMatches | ForEach-Object { Write-Host "    $($_.Line)" -ForegroundColor DarkRed }
    }
    if ($totalFailures -gt 0) {
        Write-Host "  - Harmony Failures: $totalFailures" -ForegroundColor Red
        $harmonyFailures | ForEach-Object { Write-Host "    $($_.Line)" -ForegroundColor DarkRed }
    }
    exit 1
}
