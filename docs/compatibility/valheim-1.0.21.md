# Valheim 1.0.21 fleet compatibility evidence

- Status: **PASS**
- Run: `20261030T115435Z`
- Host: `omen-linux`
- Completed: 2026-10-30 12:03:24 UTC

## Game baseline

- Valheim version detected from `Version..cctor`: `1.0.21`
- Steam build: `26249040`
- `assembly_valheim.dll` SHA-256: `C39A1F03D7918B725366E711CDCB08F72F637FC675877856D4B48C0B5092D882`
- BepInEx package dependency: `denikson-BepInExPack_Valheim-5.4.2202` (installed runtime: BepInEx 5.4.23.3 / pack 5.4.2333).
- Test method: Release builds, Cecil hook/reflection audits, one isolated main-menu scene boot per mod, BepInEx and Unity error scans, `tcli` package validation
- Profile restoration: `BepInEx/plugins` returned to `/home/derek/.local/share/Steam/steamapps/common/Valheim/BepInEx/plugins_local`

## Results

| Mod | Candidate | Declared support | Build | Hook/reflection audit | Isolated boot | DLL SHA-256 | Package SHA-256 |
|---|---:|---|---|---|---|---|---|
| IsModded | 1.0.8 | 1.0.0-1.0.21 | PASS | PASS | PASS, 12.060s | `BDC9526CF53498D06DCB3D57923C6961FFC17C6C3AFCB493087DF206F6314DC8` | `8631E4AF4FA8BA01E44DAC54DCEA600A6BB3375C883639F1F8721AD7DF81310A` |
| SelfieStick | 0.3.6 | 1.0.0-1.0.21 | PASS | PASS | PASS, 12.049s | `01DD4912192C98C371D1FACB2D449B10D61A6D4D23D79A5A4BD335C6008D42BF` | `26FA6778E0D9FAED56888D05FA41CE0EEB6C257AD0A0D47EA5FE49692A4133C6` |
| Unfaded | 1.0.12 | 1.0.0-1.0.21 | PASS | PASS | PASS, 12.041s | `9A4BEA9B524203370F4B4618FCE81EB0231FB805FC7BE97A0CF94E56D1B5D12C` | `2E2F265B80B71553CCEE710019D801076DF1BA0D0B75D16F14525ADED6B4BF5C` |
| TotemSentinel | 1.5.6 | 1.0.0-1.0.21 | PASS | PASS | PASS, 12.060s | `BF355A26821D97B8CE34B38069C46D98E590BAB9BD98702154D4CD0C00CD4838` | `EF7B5E637DD308CCEC35703AA9DEAEEED50B3BC085D3D6FD1E064CC2173326B5` |
| Unswayed | 1.0.6 | 1.0.0-1.0.21 | PASS | PASS | PASS, 12.042s | `C11411D1E8346C36B0DB99F091A7F94717754AE822B6E1588EED91B5C7A79750` | `6C2B994050DB4C2910E3E107383EBE9C0D9FA9B67EF308FA43032A475F5E7FF3` |

Every final boot contained the exact `Loading [<plugin> <candidate>]` record, `Chainloader startup complete`, Steam initialization and the Unity `Amb_MainMenu` scene marker. BepInEx and Unity logs have zero matched errors, specified exceptions or Harmony failures. Boot time is measured from process launch to these conditions with a 12-second minimum observation window; shutdown uses SIGTERM.

The exact five ZIPs were checked against Thunderstore's live API. Each candidate is newer than its published package: IsModded 1.0.7, SelfieStick 0.3.5, Unfaded 1.0.11, TotemSentinel 1.5.5, Unswayed 1.0.5.

## Patch-specific findings

- IsModded 1.0.8: achievement, startup, inventory and terminal Harmony targets bind; `Achievements.IsCheatedAtAll()` still references `Game.isModded`.
- TotemSentinel 1.5.6: camp-radar hooks and sector references resolve and initialize.
- SelfieStick 0.3.6 (`Camera Proof`), Unfaded 1.0.12 and Unswayed 1.0.6: camera, HUD and movement hooks resolve and initialize.

## Scope

This evidence supports package compatibility and clean initialization on 1.0.21. It does not claim a full gameplay regression pass. Achievement unlock, death/respawn, live sonar, crypt camera and photographic capture scenarios should be repeated when feature code changes.

Early collection attempts exposed desktop authorization/Steam setup problems and an overly narrow main-menu sentinel. They are retained under `setup-attempt-*` and excluded from the final matrix. The Linux suite link previously pointed into the read-only Windows mount; local play now uses a writable Linux copy with the five verified DLLs. The Windows suite was not changed.

Raw evidence: `artifacts/compatibility/valheim-1.0.21/20261030T115435Z/`.

## Thunderstore release

Candidate ZIPs are built, runtime-verified on `omen-linux`, and validated against the live Thunderstore API. They are **not published**. The exact ZIPs, matching `.thunderstore.toml` sidecars and validation receipt remain local under `artifacts/compatibility/valheim-1.0.21/20261030T115435Z/packages/`; raw artifacts are ignored by Git and are not included in this commit.

Publishing these exact ZIPs requires `THUNDERSTORE_TOKEN` with `tools/Publish-ThunderstoreArtifacts.ps1 -ArtifactsDirectory artifacts/compatibility/valheim-1.0.21/20261030T115435Z/packages -Token $env:THUNDERSTORE_TOKEN -Confirm PUBLISH` on a PowerShell-capable host. Alternatively, the existing `thunderstore-release.yml` workflow requires the `OMEN-valheim` Windows runner for preparation and the `thunderstore-production` approval for publishing. That workflow is unchanged and has no `evidence_run_id` input or Linux-evidence reuse path; adding one requires a `workflow`-scoped credential. No release workflow was launched during this verification.
