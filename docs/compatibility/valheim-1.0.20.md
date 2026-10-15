# Valheim 1.0.20 fleet compatibility evidence

- Status: **PASS**
- Run: `20261015T183737Z`
- Host: `OMEN`
- Completed: 2026-10-15 18:39:04 UTC

## Game baseline

- Valheim version detected from `Version..cctor`: `1.0.20`
- Steam build: `25909357`
- `assembly_valheim.dll` SHA-256: `94998885B103D6E5F5480123EAEE0A093E82AF2DDEEEE37536C15BF555F24F3A`
- BepInEx package dependency: `denikson-BepInExPack_Valheim-5.4.2202`
- Test method: release build, Cecil hook/reflection audit, one isolated main-menu boot per mod, BepInEx error scan, `tcli` package validation
- Profile restoration: `BepInEx/plugins` returned to `BepInEx/profiles/sovereign-suite`

## Results

| Mod | Candidate | Declared support | Build | Hook/reflection audit | Isolated boot | DLL SHA-256 | Package SHA-256 |
|---|---:|---|---|---|---|---|---|
| IsModded | 1.0.7 | 1.0.0-1.0.20 | PASS | PASS | PASS, 12.70s | `BAA240962A9CD114632F5039C4A036F91F02D46A3DB8C0E218D41CD0D359129C` | `36FB9E2E20BCC65F0EF54D51DAAD64E158B1841208DBD1C40018F689B38D29D3` |
| SelfieStick | 0.3.5 | 1.0.0-1.0.20 | PASS | PASS | PASS, 12.65s | `DE0085DDF0A641960D8325671798731808EEACFCF06D0131A991338C29E2F352` | `D3E849F686254895C921048696CEA6301B3EE828CE1BB53891E4C29D86398F88` |
| Unfaded | 1.0.11 | 1.0.0-1.0.20 | PASS | PASS | PASS, 12.65s | `09F019974BFDFA7AA9A810F5D56F059A47187A945215EB912DF46E8D6A4BCC22` | `8C9A8995D4F09D179BC573103B71FBAD1EE8160132C02685AB71A2953BB4CE37` |
| TotemSentinel | 1.5.5 | 1.0.0-1.0.20 | PASS | PASS | PASS, 12.68s | `EB1C72E3883DD61F3AAA707934244F1BDF06CDDE0FA74CC2F9A76BAE80E2BC16` | `66B5188817560ADAD0DD3288D3607F68352A4EAC0E7FDF919E95E9106E01AC9C` |
| Unswayed | 1.0.5 | 1.0.0-1.0.20 | PASS | PASS | PASS, 12.65s | `4FBD1EFA51E4B2F6AD7260701993F4DD48E0FFA76FBBC3CA7E610B0E4ACF1CF2` | `EC47E8534C0273E37069D3D57A9EF26F64460BABACDF9745EC9B1991F5CE90CA` |

Every boot contained the exact `Loading [<plugin> <candidate>]` BepInEx record and completed with zero matched errors, exceptions, or Harmony failures.

The exact five ZIPs above were also checked against Thunderstore's live API. Each candidate is newer than the currently published package: IsModded 1.0.6, SelfieStick 0.3.4, TotemSentinel 1.5.4, Unfaded 1.0.10, and Unswayed 1.0.4.

## Patch-specific findings

All declared Harmony hooks, IL references, and reflection surfaces across the fleet remain 100% binary-compatible with Valheim 1.0.20:
- `Achievements.IsCheatedAtAll()` continues to read `Game.isModded` in 1.0.20. IsModded 1.0.7 successfully isolates this check without altering Iron Gate's cheat/bypass logic.
- TotemSentinel 1.5.5 resolves camp radar sectors via `m_width` and `ZoneSystem.SectorToIndex` without regression.
- SelfieStick 0.3.5, Unfaded 1.0.11, and Unswayed 1.0.5 camera, HUD, death, and locomotion hooks all bound and loaded cleanly.

## Scope

This evidence supports package compatibility and clean initialization on 1.0.20. It does not claim a full gameplay regression pass. Achievement unlock, death/respawn, live sonar, crypt camera, and photographic capture scenarios should be repeated when their feature code changes.

The raw run output is generated locally under `artifacts/compatibility/valheim-1.0.20/20261015T183737Z/`; CI uploads the equivalent directory as a 30-day GitHub Actions artifact.
