<#
.SYNOPSIS
    Builds, audits, runtime-tests, packages, and optionally publishes the djcdevelopment Valheim fleet.
.DESCRIPTION
    Uses Valheim Profile Manager synthetic profiles for isolated boot tests. Every run verifies the
    installed game version, restores the exact original BepInEx/plugins junction target, validates
    release metadata, and writes machine-readable evidence before a package can be published.
#>
[CmdletBinding()]
param(
    [switch]$Audit,
    [switch]$Build,
    [switch]$RuntimeTest,
    [switch]$Package,
    [switch]$Publish,
    [switch]$All,
    [string[]]$Mods = @(),
    [string]$TargetGameVersion,
    [string]$WorkspaceRoot,
    [string]$RepositoryRoot,
    [string]$ProfileManagerRoot,
    [string]$GamePath = "C:\Program Files (x86)\Steam\steamapps\common\Valheim",
    [string]$SteamPath = "C:\Program Files (x86)\Steam\steam.exe",
    [string]$EvidenceDirectory,
    [string]$PublishToken,
    [int]$RuntimeTimeoutSeconds = 45
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$scriptDir = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = (Resolve-Path (Join-Path $scriptDir "..")).Path
}
if ([string]::IsNullOrWhiteSpace($WorkspaceRoot)) {
    $WorkspaceRoot = Split-Path -Parent $RepositoryRoot
}
if ([string]::IsNullOrWhiteSpace($ProfileManagerRoot)) {
    $ProfileManagerRoot = Join-Path $WorkspaceRoot "valheim-profile-engine"
}

$fleetPath = Join-Path $RepositoryRoot "manifests\mod-fleet.json"
if (-not (Test-Path -LiteralPath $fleetPath)) {
    throw "Fleet manifest not found: $fleetPath"
}
$fleet = Get-Content -LiteralPath $fleetPath -Raw -Encoding UTF8 | ConvertFrom-Json
if ([string]::IsNullOrWhiteSpace($TargetGameVersion)) {
    $TargetGameVersion = $fleet.target_game_version
}
if ($All) {
    $Audit = $true
    $Build = $true
    $RuntimeTest = $true
    $Package = $true
}
if (-not ($Audit -or $Build -or $RuntimeTest -or $Package -or $Publish)) {
    $Audit = $true
    $Build = $true
    $RuntimeTest = $true
    $Package = $true
}
if ($Publish -and -not $Package) {
    throw "Publishing requires -Package in the same run so the tested ZIP files are the files uploaded."
}

$selectedMods = @($fleet.mods | Where-Object {
    $Mods.Count -eq 0 -or $Mods -contains $_.name
})
if ($selectedMods.Count -eq 0) {
    throw "No fleet mods matched: $($Mods -join ', ')"
}
$unknownMods = @($Mods | Where-Object { $_ -notin @($fleet.mods.name) })
if ($unknownMods.Count -gt 0) {
    throw "Unknown mod name(s): $($unknownMods -join ', ')"
}

$runId = (Get-Date).ToUniversalTime().ToString("yyyyMMddTHHmmssZ")
if ([string]::IsNullOrWhiteSpace($EvidenceDirectory)) {
    $EvidenceDirectory = Join-Path $RepositoryRoot "artifacts\compatibility\valheim-$TargetGameVersion\$runId"
}
$EvidenceDirectory = [System.IO.Path]::GetFullPath($EvidenceDirectory)
$null = New-Item -ItemType Directory -Path $EvidenceDirectory -Force
$logsDirectory = Join-Path $EvidenceDirectory "logs"
$packagesDirectory = Join-Path $EvidenceDirectory "packages"
$null = New-Item -ItemType Directory -Path $logsDirectory -Force
$null = New-Item -ItemType Directory -Path $packagesDirectory -Force

$report = [ordered]@{
    schema_version = 1
    run_id = $runId
    started_utc = (Get-Date).ToUniversalTime().ToString("o")
    finished_utc = $null
    status = "running"
    error = $null
    game = [ordered]@{
        name = "Valheim"
        expected_version = $TargetGameVersion
        detected_version = $null
        assembly_sha256 = $null
        assembly_last_write_utc = $null
        steam_build_id = $null
    }
    environment = [ordered]@{
        machine = $env:COMPUTERNAME
        game_path = $GamePath
        profile_manager_root = $ProfileManagerRoot
        workspace_root = $WorkspaceRoot
        original_profile_target = $null
        original_profile_name = $null
    }
    stages = New-Object System.Collections.ArrayList
    mods = New-Object System.Collections.ArrayList
}

function Add-StageResult {
    param([string]$Name, [string]$Status, [string]$Detail)
    $null = $report.stages.Add([ordered]@{ name = $Name; status = $Status; detail = $Detail })
}

function Get-Sha256 {
    param([Parameter(Mandatory = $true)][string]$LiteralPath)
    if (Get-Command Get-FileHash -ErrorAction SilentlyContinue) {
        return (Get-FileHash -LiteralPath $LiteralPath -Algorithm SHA256).Hash
    }
    $stream = [System.IO.File]::OpenRead($LiteralPath)
    try {
        $hasher = [System.Security.Cryptography.SHA256]::Create()
        return ([System.BitConverter]::ToString($hasher.ComputeHash($stream)) -replace '-','').ToUpperInvariant()
    } finally {
        $stream.Dispose()
    }
}

function Invoke-Checked {
    param(
        [Parameter(Mandatory = $true)][string]$FilePath,
        [Parameter(Mandatory = $true)][string[]]$ArgumentList,
        [string]$WorkingDirectory
    )
    if ($WorkingDirectory) { Push-Location $WorkingDirectory }
    try {
        $output = & $FilePath @ArgumentList 2>&1
        $exitCode = $LASTEXITCODE
        $output | ForEach-Object { Write-Host $_ }
        if ($exitCode -ne 0) {
            throw "$FilePath exited with code $exitCode"
        }
        return ($output -join [Environment]::NewLine)
    }
    finally {
        if ($WorkingDirectory) { Pop-Location }
    }
}

function Resolve-ModPath {
    param($Mod, [string]$ChildPath)
    $sourceRoot = Join-Path $WorkspaceRoot ($Mod.source_relative -replace '/', '\')
    if ($ChildPath) { return Join-Path $sourceRoot ($ChildPath -replace '/', '\') }
    return $sourceRoot
}

function Get-TomlValue {
    param([string]$Path, [string]$Name)
    $text = Get-Content -LiteralPath $Path -Raw -Encoding UTF8
    $pattern = '(?m)^{0}\s*=\s*[''"](?<value>[^''"]+)[''"]\s*$' -f [regex]::Escape($Name)
    $match = [regex]::Match($text, $pattern)
    if (-not $match.Success) { throw "Missing '$Name' in $Path" }
    return $match.Groups["value"].Value
}

function Test-ModMetadata {
    param($Mod)
    $root = Resolve-ModPath $Mod ""
    $projectPath = Resolve-ModPath $Mod $Mod.project
    $manifestPath = Join-Path $root "manifest.json"
    $tomlPath = Resolve-ModPath $Mod $Mod.package_config
    $readmePath = Join-Path $root "README.md"
    foreach ($path in @($root, $projectPath, $manifestPath, $tomlPath, $readmePath)) {
        if (-not (Test-Path -LiteralPath $path)) { throw "$($Mod.name): required path missing: $path" }
    }

    [xml]$project = Get-Content -LiteralPath $projectPath -Raw -Encoding UTF8
    $versionNode = $project.SelectSingleNode("//Version")
    if ($null -eq $versionNode) { throw "$($Mod.name): <Version> missing from $projectPath" }
    $projectVersion = $versionNode.InnerText.Trim()
    $packageManifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $tomlVersion = Get-TomlValue $tomlPath "versionNumber"
    $versions = [ordered]@{
        fleet = [string]$Mod.version
        project = $projectVersion
        manifest = [string]$packageManifest.version_number
        toml = $tomlVersion
    }
    $mismatches = @($versions.GetEnumerator() | Where-Object { $_.Value -ne $Mod.version })
    if ($mismatches.Count -gt 0) {
        throw "$($Mod.name): version mismatch: $(($versions.GetEnumerator() | ForEach-Object { "$($_.Key)=$($_.Value)" }) -join ', ')"
    }

    $sourceFiles = Get-ChildItem -LiteralPath $root -Recurse -File -Filter "*.cs" | Where-Object {
        $_.FullName -notmatch '[\\/](bin|obj)[\\/]'
    }
    $declaredVersions = New-Object System.Collections.Generic.List[string]
    foreach ($sourceFile in $sourceFiles) {
        $source = Get-Content -LiteralPath $sourceFile.FullName -Raw -Encoding UTF8
        foreach ($match in [regex]::Matches($source, '(?:PluginVersion|CaptureVersion)\s*=\s*"(?<version>\d+\.\d+\.\d+)"')) {
            $declaredVersions.Add($match.Groups["version"].Value)
        }
        foreach ($match in [regex]::Matches($source, 'BepInPlugin\([^\)]*"(?<version>\d+\.\d+\.\d+)"\s*\)')) {
            $declaredVersions.Add($match.Groups["version"].Value)
        }
    }
    $declaredVersions = @($declaredVersions | Sort-Object -Unique)
    if ($declaredVersions.Count -eq 0 -or -not ($declaredVersions -contains [string]$Mod.version)) {
        throw "$($Mod.name): no BepInEx/source version declaration matches $($Mod.version)"
    }
    $conflicts = @($declaredVersions | Where-Object { $_ -ne $Mod.version })
    if ($conflicts.Count -gt 0) {
        throw "$($Mod.name): conflicting source version declaration(s): $($conflicts -join ', ')"
    }

    $description = [string]$packageManifest.description
    if (-not $description.Contains($TargetGameVersion)) {
        throw "$($Mod.name): manifest description does not name tested Valheim $TargetGameVersion"
    }
    $readme = Get-Content -LiteralPath $readmePath -Raw -Encoding UTF8
    if (-not $readme.Contains($TargetGameVersion) -or -not $readme.Contains($Mod.version)) {
        throw "$($Mod.name): README must name game $TargetGameVersion and package $($Mod.version)"
    }
    return $versions
}

function Get-SteamBuildId {
    $steamApps = Split-Path -Parent (Split-Path -Parent $GamePath)
    $appManifest = Join-Path $steamApps "appmanifest_892970.acf"
    if (-not (Test-Path -LiteralPath $appManifest)) { return $null }
    $raw = Get-Content -LiteralPath $appManifest -Raw
    $match = [regex]::Match($raw, '"buildid"\s+"(?<id>\d+)"')
    if ($match.Success) { return $match.Groups["id"].Value }
    return $null
}

function Stop-Valheim {
    $processes = @(Get-Process valheim* -ErrorAction SilentlyContinue)
    if ($processes.Count -gt 0) {
        $processes | Stop-Process -Force
        $processes | ForEach-Object { try { $_.WaitForExit(10000) } catch {} }
    }
}

function Invoke-RuntimeMatrix {
    $switchScript = Join-Path $ProfileManagerRoot "tools\switch-profile.ps1"
    $verifyScript = Join-Path $ProfileManagerRoot "tools\Verify-ProfileState.ps1"
    $harnessScript = Join-Path $RepositoryRoot "harness\Start-ValheimHarness.ps1"
    foreach ($path in @($switchScript, $verifyScript, $harnessScript, $SteamPath)) {
        if (-not (Test-Path -LiteralPath $path)) { throw "Runtime dependency missing: $path" }
    }
    if (Get-Process valheim* -ErrorAction SilentlyContinue) {
        throw "Valheim is already running; refusing to replace the active plugin profile."
    }

    $pluginsPath = Join-Path $GamePath "BepInEx\plugins"
    $pluginsItem = Get-Item -LiteralPath $pluginsPath -Force
    if (-not ($pluginsItem.Attributes -band [System.IO.FileAttributes]::ReparsePoint)) {
        throw "BepInEx/plugins is not managed by Valheim Profile Manager: $pluginsPath"
    }
    $originalTarget = @($pluginsItem.Target)[0]
    if ([string]::IsNullOrWhiteSpace($originalTarget)) {
        throw "Could not resolve the original Valheim Profile Manager junction target."
    }
    if (-not [System.IO.Path]::IsPathRooted($originalTarget)) {
        $originalTarget = [System.IO.Path]::GetFullPath((Join-Path (Split-Path -Parent $pluginsPath) $originalTarget))
    }
    $report.environment.original_profile_target = $originalTarget

    $profileManagerManifestPath = Join-Path $ProfileManagerRoot "manifests\profiles.json"
    $originalProfileName = $null
    $selectedOutputPaths = @($selectedMods | ForEach-Object {
        [System.IO.Path]::GetFullPath((Resolve-ModPath $_ $_.output))
    })
    if (Test-Path -LiteralPath $profileManagerManifestPath) {
        $profileManagerManifest = Get-Content -LiteralPath $profileManagerManifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
        foreach ($profileProperty in $profileManagerManifest.profiles.PSObject.Properties) {
            $targetProperty = $profileProperty.Value.PSObject.Properties["target_dir"]
            $pathProperty = $profileProperty.Value.PSObject.Properties["path"]
            $candidateTarget = if ($null -ne $targetProperty -and $targetProperty.Value) {
                [string]$targetProperty.Value
            }
            elseif ($null -ne $pathProperty -and $pathProperty.Value) {
                [string]$pathProperty.Value
            }
            else { $null }
            if ($candidateTarget -and [System.IO.Path]::GetFullPath($candidateTarget) -ieq $originalTarget) {
                $profileType = [string]$profileProperty.Value.type
                if ($profileType -eq "synthetic") {
                    $entrySources = @($profileProperty.Value.entries | ForEach-Object {
                        $sourcePath = [string]$_.source
                        if (-not [System.IO.Path]::IsPathRooted($sourcePath)) {
                            $sourcePath = Join-Path $ProfileManagerRoot $sourcePath
                        }
                        [System.IO.Path]::GetFullPath($sourcePath)
                    })
                    $allSourcesExist = @($entrySources | Where-Object { -not (Test-Path -LiteralPath $_) }).Count -eq 0
                    $refreshesCurrentBuild = $false
                    foreach ($entrySource in $entrySources) {
                        if ($selectedOutputPaths -icontains $entrySource) {
                            $refreshesCurrentBuild = $true
                            break
                        }
                    }
                    if ($allSourcesExist -and $refreshesCurrentBuild) {
                        $originalProfileName = $profileProperty.Name
                        break
                    }
                }
            }
        }
    }
    $report.environment.original_profile_name = $originalProfileName

    $profilesRoot = [System.IO.Path]::GetFullPath((Join-Path $GamePath "BepInEx\profiles"))
    $temporaryProfilesRoot = [System.IO.Path]::GetFullPath((Join-Path $profilesRoot "compatibility-$runId"))
    if (-not $temporaryProfilesRoot.StartsWith($profilesRoot + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Temporary profile path escaped the Valheim profiles directory: $temporaryProfilesRoot"
    }
    $null = New-Item -ItemType Directory -Path $temporaryProfilesRoot -Force

    $profiles = [ordered]@{}
    foreach ($mod in $selectedMods) {
        $outputPath = Resolve-ModPath $mod $mod.output
        $profileTarget = Join-Path $temporaryProfilesRoot $mod.name
        $profiles[$mod.profile] = [ordered]@{
            alias = @("compat-$($mod.name.ToLowerInvariant())")
            description = "Ephemeral $TargetGameVersion compatibility profile for $($mod.name)"
            type = "synthetic"
            target_dir = $profileTarget
            entries = @([ordered]@{
                name = [System.IO.Path]::GetFileName($outputPath)
                source = $outputPath
                link_type = "hardlink"
            })
        }
    }
    $profiles["restore-original"] = [ordered]@{
        alias = @("restore")
        description = "Exact profile junction target captured before compatibility tests"
        type = "directory"
        path = $originalTarget
    }
    $runtimeManifest = [ordered]@{
        version = "1.0.0"
        active_profile = "restore-original"
        default_machine = "compatibility"
        machines = [ordered]@{
            compatibility = [ordered]@{
                game_dir = $GamePath
                bepinex_dir = (Join-Path $GamePath "BepInEx")
            }
        }
        profiles = $profiles
    }
    $runtimeManifestPath = Join-Path $EvidenceDirectory "runtime-profiles.json"
    [System.IO.File]::WriteAllText($runtimeManifestPath, ($runtimeManifest | ConvertTo-Json -Depth 12), [System.Text.UTF8Encoding]::new($false))

    try {
        foreach ($mod in $selectedMods) {
            Write-Host "`n[RUNTIME] $($mod.name) via Valheim Profile Manager profile '$($mod.profile)'" -ForegroundColor Yellow
            Stop-Valheim
            Invoke-Checked "powershell" @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", $switchScript, "-ManifestPath", $runtimeManifestPath, "-Profile", $mod.profile) | Out-Null
            Invoke-Checked "powershell" @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", $verifyScript, "-ManifestPath", $runtimeManifestPath) | Out-Null

            $started = Get-Date
            try {
                Invoke-Checked "powershell" @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", $harnessScript, "-SteamPath", $SteamPath, "-TimeoutSeconds", "$RuntimeTimeoutSeconds", "-ClearLogs", "-AssertClean", "-SkipVerify") | Out-Null
                $logPath = Join-Path $GamePath "BepInEx\LogOutput.log"
                if (-not (Test-Path -LiteralPath $logPath)) { throw "$($mod.name): BepInEx log was not created" }
                $logText = Get-Content -LiteralPath $logPath -Raw -Encoding UTF8
                $expectedLoad = "Loading [$($mod.plugin_name) $($mod.version)]"
                if (-not $logText.Contains($expectedLoad)) {
                    throw "$($mod.name): expected '$expectedLoad' was not present in LogOutput.log"
                }
                $logEvidencePath = Join-Path $logsDirectory "$($mod.name).log"
                Copy-Item -LiteralPath $logPath -Destination $logEvidencePath -Force
                $dllPath = Resolve-ModPath $mod $mod.output
                $dllHash = Get-Sha256 -LiteralPath $dllPath
                $duration = [math]::Round(((Get-Date) - $started).TotalSeconds, 2)
                $null = $report.mods.Add([ordered]@{
                    name = $mod.name
                    package_version = $mod.version
                    supported_game_versions = $fleet.supported_game_version_range
                    build = "pass"
                    signature_audit = "pass"
                    isolated_boot = "pass"
                    boot_seconds = $duration
                    dll_sha256 = $dllHash
                    log = "logs/$($mod.name).log"
                    package = $null
                    package_sha256 = $null
                })
            }
            finally {
                Stop-Valheim
            }
        }
    }
    finally {
        Stop-Valheim
        try {
            if ($originalProfileName) {
                Invoke-Checked "powershell" @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", $switchScript, "-ManifestPath", $profileManagerManifestPath, "-Profile", $originalProfileName) | Out-Null
            }
            else {
                Invoke-Checked "powershell" @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", $switchScript, "-ManifestPath", $runtimeManifestPath, "-Profile", "restore-original") | Out-Null
            }
            $restoredItem = Get-Item -LiteralPath $pluginsPath -Force
            $restoredTarget = [System.IO.Path]::GetFullPath(@($restoredItem.Target)[0])
            if ($restoredTarget -ine $originalTarget) {
                throw "Profile restoration target mismatch: expected '$originalTarget', found '$restoredTarget'"
            }
        }
        finally {
            if (Test-Path -LiteralPath $temporaryProfilesRoot) {
                Remove-Item -LiteralPath $temporaryProfilesRoot -Recurse -Force
            }
        }
    }
}

function Test-PackageArchive {
    param($Mod, [string]$PackagePath)
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [System.IO.Compression.ZipFile]::OpenRead($PackagePath)
    try {
        $required = @("manifest.json", "README.md", "icon.png", [System.IO.Path]::GetFileName((Resolve-ModPath $Mod $Mod.output)))
        $names = @($archive.Entries | ForEach-Object { $_.FullName })
        $missing = @($required | Where-Object { $_ -notin $names })
        if ($missing.Count -gt 0) { throw "$($Mod.name): package is missing $($missing -join ', ')" }
        $manifestEntry = $archive.Entries | Where-Object { $_.FullName -eq "manifest.json" } | Select-Object -First 1
        $reader = New-Object System.IO.StreamReader($manifestEntry.Open())
        try { $packageManifest = $reader.ReadToEnd() | ConvertFrom-Json } finally { $reader.Dispose() }
        if ($packageManifest.name -ne $Mod.name -or $packageManifest.version_number -ne $Mod.version) {
            throw "$($Mod.name): packaged manifest identity/version does not match the fleet manifest"
        }
    }
    finally {
        $archive.Dispose()
    }
}

function Write-Reports {
    $report.finished_utc = (Get-Date).ToUniversalTime().ToString("o")
    $jsonPath = Join-Path $EvidenceDirectory "compatibility.json"
    [System.IO.File]::WriteAllText($jsonPath, ($report | ConvertTo-Json -Depth 12), [System.Text.UTF8Encoding]::new($false))

    $rows = @($report.mods | ForEach-Object {
        "| $($_.name) | $($_.package_version) | $($_.supported_game_versions) | $($_.build) | $($_.signature_audit) | $($_.isolated_boot) | ``$($_.dll_sha256)`` |"
    })
    $lines = @(
        "# Valheim $TargetGameVersion compatibility evidence",
        "",
        "- Run: ``$runId``",
        "- Status: **$($report.status)**",
        "- Steam build: ``$($report.game.steam_build_id)``",
        "- Game assembly SHA-256: ``$($report.game.assembly_sha256)``",
        "- Valheim Profile Manager restored profile: ``$($report.environment.original_profile_name)``",
        "- Valheim Profile Manager restored target: ``$($report.environment.original_profile_target)``",
        "",
        "| Mod | Candidate | Supported Valheim | Build | Hook audit | Isolated boot | DLL SHA-256 |",
        "|---|---:|---|---|---|---|---|"
    ) + $rows + @(
        "",
        "Raw BepInEx logs and package ZIPs are stored beside this report. Publishing is a separate protected step and consumes these exact ZIPs."
    )
    $markdownPath = Join-Path $EvidenceDirectory "compatibility.md"
    [System.IO.File]::WriteAllText($markdownPath, ($lines -join [Environment]::NewLine), [System.Text.UTF8Encoding]::new($false))
    Write-Host "`nEvidence: $markdownPath" -ForegroundColor Cyan
}

Write-Host "=====================================================================" -ForegroundColor Cyan
Write-Host " Valheim $TargetGameVersion Compatibility & Thunderstore Pipeline" -ForegroundColor Cyan
Write-Host "=====================================================================" -ForegroundColor Cyan
Write-Host "Mods: $($selectedMods.name -join ', ')"
Write-Host "Evidence: $EvidenceDirectory"

try {
    foreach ($mod in $selectedMods) {
        $null = Test-ModMetadata $mod
        Write-Host "[PASS] $($mod.name) metadata is synchronized at $($mod.version)" -ForegroundColor Green
    }
    Add-StageResult "metadata" "pass" "$($selectedMods.Count) mod(s) synchronized"

    $assemblyPath = Join-Path $GamePath "valheim_Data\Managed\assembly_valheim.dll"
    if (-not (Test-Path -LiteralPath $assemblyPath)) { throw "Valheim assembly not found: $assemblyPath" }
    $assemblyItem = Get-Item -LiteralPath $assemblyPath
    $report.game.assembly_sha256 = Get-Sha256 -LiteralPath $assemblyPath
    $report.game.assembly_last_write_utc = $assemblyItem.LastWriteTimeUtc.ToString("o")
    $report.game.steam_build_id = Get-SteamBuildId

    if ($Audit) {
        $inspectorProject = Join-Path $RepositoryRoot "tools\Inspector\Inspector.csproj"
        $inspectorOutput = Invoke-Checked "dotnet" @("run", "--project", $inspectorProject, "-c", "Release", "--", $GamePath, $TargetGameVersion)
        $versionMatch = [regex]::Match($inspectorOutput, 'EVIDENCE game_version=(?<version>\d+\.\d+\.\d+)')
        if (-not $versionMatch.Success) { throw "Inspector did not report a game version" }
        $report.game.detected_version = $versionMatch.Groups["version"].Value
        if ($report.game.detected_version -ne $TargetGameVersion) {
            throw "Installed Valheim is $($report.game.detected_version); expected $TargetGameVersion"
        }
        Add-StageResult "game-signatures" "pass" "Valheim $TargetGameVersion; $($report.game.assembly_sha256)"
    }

    if ($Build) {
        foreach ($mod in $selectedMods) {
            $projectPath = Resolve-ModPath $mod $mod.project
            Invoke-Checked "dotnet" @("build", $projectPath, "-c", "Release", "--nologo", "-p:GamePath=$GamePath", "-p:ValheimDir=$GamePath", "-p:DeployToGame=false") | Out-Null
            $outputPath = Resolve-ModPath $mod $mod.output
            if (-not (Test-Path -LiteralPath $outputPath)) { throw "$($mod.name): build output missing: $outputPath" }
            $assemblyVersion = [System.Reflection.AssemblyName]::GetAssemblyName($outputPath).Version.ToString(3)
            if ($assemblyVersion -ne $mod.version) {
                throw "$($mod.name): DLL assembly version $assemblyVersion does not match $($mod.version)"
            }
            Write-Host "[PASS] $($mod.name) built with DLL version $assemblyVersion" -ForegroundColor Green
        }
        Add-StageResult "build" "pass" "$($selectedMods.Count) release DLL(s)"
    }

    if ($RuntimeTest) {
        if (-not $Audit) { throw "Runtime tests require -Audit in the same run" }
        Invoke-RuntimeMatrix
        Add-StageResult "isolated-runtime" "pass" "$($selectedMods.Count) profile(s) booted and original junction restored"
    }
    else {
        foreach ($mod in $selectedMods) {
            $dllPath = Resolve-ModPath $mod $mod.output
            if (Test-Path -LiteralPath $dllPath) {
                $null = $report.mods.Add([ordered]@{
                    name = $mod.name
                    package_version = $mod.version
                    supported_game_versions = $fleet.supported_game_version_range
                    build = if ($Build) { "pass" } else { "not-run" }
                    signature_audit = if ($Audit) { "pass" } else { "not-run" }
                    isolated_boot = "not-run"
                    boot_seconds = $null
                    dll_sha256 = (Get-Sha256 -LiteralPath $dllPath)
                    log = $null
                    package = $null
                    package_sha256 = $null
                })
            }
        }
    }

    if ($Package) {
        foreach ($mod in $selectedMods) {
            $sourceRoot = Resolve-ModPath $mod ""
            Invoke-Checked "tcli" @("build", "--config-path", $mod.package_config) $sourceRoot | Out-Null
            $packageName = "$($fleet.namespace)-$($mod.name)-$($mod.version).zip"
            $packagePath = Join-Path (Join-Path $sourceRoot "dist") $packageName
            if (-not (Test-Path -LiteralPath $packagePath)) { throw "$($mod.name): tcli did not create $packagePath" }
            Test-PackageArchive $mod $packagePath
            $candidatePath = Join-Path $packagesDirectory $packageName
            Copy-Item -LiteralPath $packagePath -Destination $candidatePath -Force
            $configSidecarName = "$([System.IO.Path]::GetFileNameWithoutExtension($packageName)).thunderstore.toml"
            $configSidecarPath = Join-Path $packagesDirectory $configSidecarName
            $sourceConfigPath = Resolve-ModPath $mod $mod.package_config
            Copy-Item -LiteralPath $sourceConfigPath -Destination $configSidecarPath -Force
            $modReport = $report.mods | Where-Object { $_.name -eq $mod.name } | Select-Object -First 1
            if ($null -ne $modReport) {
                $modReport.package = "packages/$packageName"
                $modReport.package_sha256 = Get-Sha256 -LiteralPath $candidatePath
            }
            Write-Host "[PASS] $packageName validated" -ForegroundColor Green
        }
        Add-StageResult "package" "pass" "$($selectedMods.Count) Thunderstore candidate ZIP(s)"
        $publisher = Join-Path $RepositoryRoot "tools\Publish-ThunderstoreArtifacts.ps1"
        Invoke-Checked "powershell" @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", $publisher, "-ArtifactsDirectory", $packagesDirectory, "-ValidateOnly") | Out-Null
        Add-StageResult "thunderstore-version" "pass" "Every candidate version is newer than its live package"
    }

    if ($Publish) {
        $publisher = Join-Path $RepositoryRoot "tools\Publish-ThunderstoreArtifacts.ps1"
        $token = if (-not [string]::IsNullOrWhiteSpace($PublishToken)) { $PublishToken } else { $env:THUNDERSTORE_TOKEN }
        if ([string]::IsNullOrWhiteSpace($token)) { throw "THUNDERSTORE_TOKEN or -PublishToken is required for -Publish" }
        Invoke-Checked "powershell" @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", $publisher, "-ArtifactsDirectory", $packagesDirectory, "-Token", $token, "-Confirm", "PUBLISH") | Out-Null
        Add-StageResult "publish" "pass" "$($selectedMods.Count) package(s) accepted by Thunderstore"
    }

    $report.status = "pass"
    Write-Reports
}
catch {
    $report.status = "fail"
    $report.error = $_.Exception.Message
    try { Write-Reports } catch {}
    throw
}

Write-Host "`n[PASS] Pipeline complete for Valheim $TargetGameVersion" -ForegroundColor Green
