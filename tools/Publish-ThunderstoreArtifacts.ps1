[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ArtifactsDirectory,
    [string]$Token,
    [string]$Confirm,
    [switch]$ValidateOnly,
    [string]$ReceiptPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$ArtifactsDirectory = [System.IO.Path]::GetFullPath($ArtifactsDirectory)
if (-not (Test-Path -LiteralPath $ArtifactsDirectory -PathType Container)) {
    throw "Artifacts directory not found: $ArtifactsDirectory"
}
if ([string]::IsNullOrWhiteSpace($ReceiptPath)) {
    $receiptName = if ($ValidateOnly) { "validation-receipt.json" } else { "publish-receipt.json" }
    $ReceiptPath = Join-Path $ArtifactsDirectory $receiptName
}
if (-not $ValidateOnly) {
    if ($Confirm -ne "PUBLISH") { throw "Publishing requires -Confirm PUBLISH" }
    if ([string]::IsNullOrWhiteSpace($Token)) { throw "Publishing requires a Thunderstore token" }
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
$packages = @(Get-ChildItem -LiteralPath $ArtifactsDirectory -File -Filter "djcdevelopment-*.zip" | Sort-Object Name)
if ($packages.Count -eq 0) { throw "No Thunderstore candidate ZIPs found in $ArtifactsDirectory" }

$candidates = New-Object System.Collections.ArrayList
$receipts = New-Object System.Collections.ArrayList
foreach ($package in $packages) {
    $archive = [System.IO.Compression.ZipFile]::OpenRead($package.FullName)
    try {
        $manifestEntry = $archive.Entries | Where-Object { $_.FullName -eq "manifest.json" } | Select-Object -First 1
        if ($null -eq $manifestEntry) { throw "$($package.Name): manifest.json missing" }
        $reader = New-Object System.IO.StreamReader($manifestEntry.Open())
        try { $manifest = $reader.ReadToEnd() | ConvertFrom-Json } finally { $reader.Dispose() }
    }
    finally { $archive.Dispose() }

    $apiUrl = "https://thunderstore.io/api/experimental/package/djcdevelopment/$($manifest.name)/"
    $current = Invoke-RestMethod -Uri $apiUrl
    $candidateVersion = [version]$manifest.version_number
    $liveVersion = [version]$current.latest.version_number
    if ($candidateVersion -le $liveVersion) {
        throw "$($manifest.name): candidate $candidateVersion must be newer than live $liveVersion"
    }

    $receipt = [ordered]@{
        name = $manifest.name
        version = $manifest.version_number
        previous_version = $current.latest.version_number
        status = "validated"
        sha256 = (Get-FileHash -LiteralPath $package.FullName -Algorithm SHA256).Hash
        recorded_utc = (Get-Date).ToUniversalTime().ToString("o")
        package_url = "https://thunderstore.io/c/valheim/p/djcdevelopment/$($manifest.name)/"
        error = $null
    }
    $null = $receipts.Add($receipt)
    $null = $candidates.Add([pscustomobject]@{
        Package = $package
        Name = [string]$manifest.name
        Version = $candidateVersion
        LiveVersion = $liveVersion
        Receipt = $receipt
    })
    Write-Host "Validated $($manifest.name) $candidateVersion (live: $liveVersion)" -ForegroundColor Green
}

function Write-Receipt {
    [System.IO.File]::WriteAllText(
        [System.IO.Path]::GetFullPath($ReceiptPath),
        (@{ packages = $receipts } | ConvertTo-Json -Depth 6),
        [System.Text.UTF8Encoding]::new($false)
    )
}

Write-Receipt
if (-not $ValidateOnly) {
    foreach ($candidate in $candidates) {
        Write-Host "Publishing $($candidate.Name) $($candidate.Version) (live: $($candidate.LiveVersion))" -ForegroundColor Cyan
        $candidate.Receipt.status = "attempting"
        $candidate.Receipt.recorded_utc = (Get-Date).ToUniversalTime().ToString("o")
        Write-Receipt
        try {
            & tcli publish --file $candidate.Package.FullName --token $Token
            if ($LASTEXITCODE -ne 0) { throw "tcli publish exited with code $LASTEXITCODE" }
            $candidate.Receipt.status = "published"
            $candidate.Receipt.recorded_utc = (Get-Date).ToUniversalTime().ToString("o")
            Write-Receipt
        }
        catch {
            $candidate.Receipt.status = "failed"
            $candidate.Receipt.error = $_.Exception.Message
            $candidate.Receipt.recorded_utc = (Get-Date).ToUniversalTime().ToString("o")
            Write-Receipt
            throw "$($candidate.Package.Name): $($_.Exception.Message)"
        }
    }
}
Write-Host "Thunderstore receipt: $ReceiptPath" -ForegroundColor Green
