# Valheim compatibility and Thunderstore release pipeline

The fleet pipeline validates all five `djcdevelopment` Thunderstore mods against one exact Valheim build, produces reviewable evidence, and publishes only the ZIP files produced by that successful run.

The current gate targets **Valheim 1.0.16**, Steam build `25527674`. Iron Gate released the patch on 2026-09-26: <https://www.valheimgame.com/news/patch-1-0-16/>.

## Fleet

| Package | Candidate | Source | Runtime profile |
|---|---:|---|---|
| IsModded | 1.0.5 | `C:\work\ismodded` | `isolated-ismodded` |
| SelfieStick | 0.3.3 | `C:\work\SelfieStick` | `clean-recording` |
| Unfaded | 1.0.9 | `C:\work\Unfaded` | `isolated-unfaded` |
| TotemSentinel | 1.5.3 | `C:\work\totemalert` | `isolated-totemsentinel` |
| Unswayed | 1.0.3 | `C:\work\deepnorthtesting\plugins\Unswayed` | `isolated-unswayed` |

[`manifests/mod-fleet.json`](../manifests/mod-fleet.json) is the machine-readable source of truth. Package version changes must be made there and in the mod project, BepInEx declaration, `manifest.json`, `thunderstore.toml`, and README. The metadata gate rejects drift before compiling.

## Local compatibility run

Run the full non-publishing pipeline from `deepnorthtesting`:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\Invoke-PatchPipeline.ps1 `
  -All `
  -TargetGameVersion 1.0.16
```

`-All` means audit, build, isolated runtime test, and package. It never publishes. A subset can be selected with `-Mods IsModded,Unfaded`.

The run performs these gates in order:

1. **Metadata consistency** checks the fleet manifest, project version, BepInEx version declaration, Thunderstore manifest, TOML, and README.
2. **Exact game audit** reads `Version..cctor` from `assembly_valheim.dll`, requires the requested version, hashes the assembly, and checks every Harmony/reflection surface used by the fleet.
3. **Release build** compiles each DLL against the installed game and checks its assembly version. Build-time deployment is disabled so compilation cannot alter the active profile.
4. **Isolated runtime matrix** creates ephemeral synthetic profiles through Valheim Profile Manager, launches each mod alone, requires the exact BepInEx name/version, and rejects errors, exceptions, or Harmony patch failures.
5. **Restoration** retargets `BepInEx/plugins` to the exact junction target captured before the run, even after a failed boot, then removes the ephemeral profile directories.
6. **Package validation** uses pinned `tcli`, opens every ZIP, and verifies `manifest.json`, README, icon, DLL, package identity, and version. DLL and ZIP SHA-256 hashes are recorded.
7. **Thunderstore preflight** checks every candidate against the live API and rejects the entire set before publishing if any version is stale or reused.

Evidence is written under `artifacts/compatibility/valheim-<version>/<UTC-run-id>/`. The folder contains `compatibility.json`, `compatibility.md`, raw BepInEx logs, the generated runtime profile manifest, the exact candidate ZIPs, and their matching `tcli` configuration sidecars.

The committed 1.0.16 result is in [`docs/compatibility/valheim-1.0.16.md`](compatibility/valheim-1.0.16.md) (with 1.0.15 in [`docs/compatibility/valheim-1.0.15.md`](compatibility/valheim-1.0.15.md)).

## Valheim Profile Manager contract

Runtime testing delegates profile operations to `valheim-profile-engine/tools/switch-profile.ps1` and `Verify-ProfileState.ps1`. The orchestrator does not maintain a second junction implementation.

Before the first switch, it resolves the live `BepInEx/plugins` reparse target. Each candidate DLL is hardlinked into a unique temporary profile. The `finally` path stops Valheim, asks Profile Manager to restore the captured target, verifies that the game is no longer running, and removes only the run-specific directory beneath `BepInEx/profiles`.

The pipeline refuses to run the runtime matrix if Valheim is already open or `BepInEx/plugins` is not a Profile Manager junction.

## GitHub Actions

[`valheim-compatibility.yml`](../.github/workflows/valheim-compatibility.yml) runs the same matrix manually and daily. It requires a Windows self-hosted runner labeled `valheim` with Steam, Valheim, BepInEx, .NET 8, and network access; the workflow installs or updates the pinned Thunderstore CLI. A scheduled failure after a game update is intentional: the exact-version gate prevents stale compatibility claims.

[`thunderstore-release.yml`](../.github/workflows/thunderstore-release.yml) has two jobs:

1. `prepare` checks out the five sources plus Valheim Profile Manager, runs the complete compatibility pipeline, and uploads immutable evidence and candidate ZIPs.
2. `publish` downloads those exact artifacts and calls `tcli publish --file`. It runs only when the dispatcher selects `publish: true` and after approval of the `thunderstore-production` GitHub environment.

Configure `THUNDERSTORE_TOKEN` as an environment secret on `thunderstore-production`. Protect that environment with required reviewers. No token is exposed to the self-hosted test job.

## Direct publishing

For a local release after reviewing the evidence:

```powershell
$env:THUNDERSTORE_TOKEN = '<token>'
powershell -NoProfile -ExecutionPolicy Bypass -File tools\Publish-ThunderstoreArtifacts.ps1 `
  -ArtifactsDirectory artifacts\compatibility\valheim-1.0.16\<run-id>\packages `
  -Token $env:THUNDERSTORE_TOKEN `
  -Confirm PUBLISH
```

The publisher opens every ZIP and compares the full set with Thunderstore's live API before the first upload. It rejects reused or older versions, uploads each exact file, and updates `publish-receipt.json` after every attempt with the hash, status, and package URL.

## Evidence limits

The automated runtime gate proves that each exact DLL initializes on the target game build without loader, Harmony, or immediate runtime errors. It does not substitute for gameplay scenarios such as earning a Steam achievement, completing an Unfaded death cycle, firing a TotemSentinel scan, walking a crypt with Unswayed, or completing a SelfieStick capture. Those scenarios remain release-review checks when behavior changed; a game-only patch with unchanged audited surfaces can use the automated evidence as the compatibility gate.
