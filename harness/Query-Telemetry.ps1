<#
.SYNOPSIS
    Queries live Valheim 1.0 in-game telemetry from the local MCP Gateway.
.DESCRIPTION
    Retrieves real-time render performance (FPS, frame time), network state,
    and region telemetry from ComfyNetworkSense running inside Valheim.
#>
param (
    [string]$GatewayUrl = "http://127.0.0.1:8721/valheim/report"
)

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "   DeepNorthTesting :: Live GPU & In-Game Telemetry       " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

try {
    $report = Invoke-RestMethod -Uri $GatewayUrl -Method Get -TimeoutSec 3
    if ($report -and $report.latest) {
        $l = $report.latest
        $avg = $report.averages

        Write-Host '[OK] Live Connection Established to ComfyNetworkSense' -ForegroundColor Green
        Write-Host "  - Session ID    : $($l.session_id)"
        Write-Host "  - Mode          : $($l.mode)"
        Write-Host "  - Region / Area : $($l.region_id)"
        Write-Host "  - Total Samples : $($report.sample_count)"
        Write-Host ''
        Write-Host '--- Real-Time GPU and Engine Metrics ---' -ForegroundColor Yellow
        Write-Host "  - Instant FPS   : $([math]::Round($l.fps, 2))" -ForegroundColor Green
        Write-Host "  - Avg FPS       : $([math]::Round($avg.fps, 2))" -ForegroundColor Green
        Write-Host "  - Frame Time    : $([math]::Round($l.frame_time_ms, 2)) ms"
        Write-Host "  - Frame Time P95: $([math]::Round($avg.frame_time_p95_ms, 2)) ms"
        Write-Host "  - CPU Bound Est : $([math]::Round($avg.cpu_bound_estimate * 100, 1))%"
        Write-Host "  - Latency (RTT) : $($l.rtt_ms) ms"
    } else {
        Write-Host "[!] Gateway responded, but no live telemetry samples received yet." -ForegroundColor Yellow
    }
} catch {
    Write-Error "[✗] Failed to connect to gateway at $GatewayUrl. Ensure the gateway process is running."
}
